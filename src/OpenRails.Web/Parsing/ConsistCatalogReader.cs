using OpenRails.Web.Content;
using Orts.Parsers.Msts;

namespace OpenRails.Web.Parsing;

public sealed record ConsistCatalogIssue(string Path, string Message);
public sealed record ConsistCarAssetReference(string ConsistName, int Uid, string CarName, string Folder, bool IsEngine, bool IsEndOfTrain, string AssetPath, bool Found, string? SourcePackage, RollingStockMetadata? Metadata, string? ShapeAssetPath = null, bool ShapeFound = false, string? ShapeSourcePackage = null, LocomotiveTractionMetadata? TractionMetadata = null, CabViewDefinition? CabView = null);
public sealed record ConsistTcsAssetReference(string ConsistName, string FileName, string AssetPath, bool Found, string? SourcePackage);
public sealed record ConsistCatalogResult(IReadOnlyList<ConsistMetadata> Consists, IReadOnlyList<ConsistCatalogIssue> Issues, IReadOnlyList<ConsistCarAssetReference> CarAssets, IReadOnlyList<ConsistTcsAssetReference> TcsAssets);

/// <summary>Discover and parse consist files in the browser virtual content namespace.</summary>
public sealed class ConsistCatalogReader
{
    public ConsistCatalogResult Read(IVirtualContentStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        var reader = new ConsistReader();
        var consists = new List<ConsistMetadata>();
        var issues = new List<ConsistCatalogIssue>();
        var carAssets = new List<ConsistCarAssetReference>();
        var tcsAssets = new List<ConsistTcsAssetReference>();
        foreach (var path in store.Paths.Where(path => path.EndsWith(".con", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var consist = reader.Read(store, path);
                var consistSourcePackage = store is CompositeContentStore consistComposite ? consistComposite.GetSourcePackage(path) : null;
                consist = consist with { SourcePackage = consistSourcePackage };
                consists.Add(consist);
                if (!string.IsNullOrWhiteSpace(consist.TrainControlSystemParametersFile))
                {
                    var tcsPath = VirtualPath.Normalize($"TRAINS/CONSISTS/SCRIPT/{consist.TrainControlSystemParametersFile}");
                    var tcsFound = store.Contains(tcsPath);
                    var tcsSourcePackage = store is CompositeContentStore tcsComposite && tcsFound ? tcsComposite.GetSourcePackage(tcsPath) : null;
                    tcsAssets.Add(new ConsistTcsAssetReference(consist.Name, consist.TrainControlSystemParametersFile, tcsPath, tcsFound, tcsSourcePackage));
                }
                foreach (var car in consist.Cars)
                {
                    var assetPath = car.IsEndOfTrain
                        ? VirtualPath.Normalize($"TRAINS/ORTS_EOT/{car.Folder}/{car.Name}.eot")
                        : VirtualPath.Normalize($"TRAINS/TRAINSET/{car.Folder}/{car.Name}{(car.IsEngine ? ".eng" : ".wag")}");
                    var found = store.Contains(assetPath);
                    var sourcePackage = store is CompositeContentStore composite && found ? composite.GetSourcePackage(assetPath) : null;
                    RollingStockMetadata? metadata = null;
                    LocomotiveTractionMetadata? tractionMetadata = null;
                    CabViewDefinition? cabView = null;
                    if (found && !car.IsEndOfTrain)
                    {
                        try
                        {
                            metadata = new RollingStockMetadataReader().Read(store, assetPath);
                        }
                        catch (Exception exception) when (exception is InvalidDataException or IOException or ArgumentException or STFException)
                        {
                            issues.Add(new ConsistCatalogIssue(assetPath, $"Rolling-stock metadata could not be read: {exception.Message}"));
                        }

                        if (car.IsEngine)
                        {
                            try
                            {
                                var cabPath = new CabViewAssetLocator().Locate(store, assetPath);
                                if (cabPath is not null)
                                    cabView = new CabViewDefinitionReader().Read(store, cabPath);
                            }
                            catch (Exception exception) when (exception is InvalidDataException or IOException or ArgumentException or STFException)
                            {
                                issues.Add(new ConsistCatalogIssue(assetPath, $"Cab-view definition could not be read: {exception.Message}"));
                            }

                            try
                            {
                                tractionMetadata = new LocomotiveTractionMetadataReader().Read(store, assetPath);
                            }
                            catch (Exception exception) when (exception is InvalidDataException or IOException or ArgumentException or STFException)
                            {
                                issues.Add(new ConsistCatalogIssue(assetPath, $"Locomotive traction metadata could not be read: {exception.Message}"));
                            }
                        }
                    }
                    RollingStockShapeAssetReference? shapeAsset = null;
                    if (metadata is not null && !string.IsNullOrWhiteSpace(metadata.ShapeFileName))
                    {
                        try
                        {
                            shapeAsset = new RollingStockShapeAssetLocator().Locate(store, car.Folder, metadata.ShapeFileName);
                        }
                        catch (InvalidDataException exception)
                        {
                            issues.Add(new ConsistCatalogIssue(assetPath, $"Unsafe WagonShape reference: {exception.Message}"));
                        }
                    }
                    var shapeSourcePackage = store is CompositeContentStore shapeComposite && shapeAsset is not null
                        ? shapeComposite.GetSourcePackage(shapeAsset.Path)
                        : null;
                    carAssets.Add(new ConsistCarAssetReference(
                        consist.Name, car.Uid, car.Name, car.Folder, car.IsEngine, car.IsEndOfTrain,
                        assetPath, found, sourcePackage, metadata,
                        shapeAsset?.Path, shapeAsset is not null, shapeSourcePackage, tractionMetadata, cabView));
                }
                foreach (var duplicate in consist.Cars.GroupBy(car => car.Uid).Where(group => group.Count() > 1))
                    issues.Add(new ConsistCatalogIssue(path, $"Duplicate car UID {duplicate.Key} appears {duplicate.Count()} times in consist '{consist.Name}'."));
                for (var carIndex = 0; carIndex < consist.Cars.Count - 1; carIndex++)
                {
                    if (consist.Cars[carIndex].IsEndOfTrain)
                        issues.Add(new ConsistCatalogIssue(path, $"End-of-train device at car position {carIndex + 1} is not the final car in consist '{consist.Name}'."));
                }
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException or ArgumentException or STFException)
            {
                issues.Add(new ConsistCatalogIssue(path, exception.Message));
            }
        }
        return new ConsistCatalogResult(Array.AsReadOnly(consists.ToArray()), Array.AsReadOnly(issues.ToArray()), Array.AsReadOnly(carAssets.ToArray()), Array.AsReadOnly(tcsAssets.ToArray()));
    }
}
