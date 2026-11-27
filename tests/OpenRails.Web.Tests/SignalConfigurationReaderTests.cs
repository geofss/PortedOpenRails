using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class SignalConfigurationReaderTests
{
    [Fact]
    public void ReadsSignalTypeFunctionsLightsDrawStatesAspectsAndScripts()
    {
        using var store = CreateStore(("ROUTES/RouteA/sigcfg.dat", """
            SIMISA@@@@@@@@@@JINX0t1t______
            ORTSSignalFunctions ( 1 ORTSSignalFunctionType ( DISTANT DISTANCE ) )
            ORTSNormalSubtypes ( 1 ORTSNormalSubtype ( MAIN ) )
            SignalTypes ( 1
                SignalType ( MainSignal
                    ORTSScript ( MainScript )
                    SignalFnType ( NORMAL )
                    SignalLightTex ( default )
                    ORTSNormalSubtype ( MAIN )
                    SignalFlags ( ABS )
                    SignalNumClearAhead ( 2 )
                    SignalLights ( 1
                        SignalLight ( 0 Red Radius ( 0.25 ) Position ( 1 2 3 ) )
                    )
                    SignalDrawStates ( 1
                        SignalDrawState ( 0 Stop
                            DrawLights ( 1 DrawLight ( 0 SignalFlags ( flashing ) ) )
                            SemaphorePos ( 0.5 )
                        )
                    )
                    SignalAspects ( 1
                        SignalAspect ( STOP Stop SpeedKPH ( 0 ) SignalFlags ( ASAP OR_SpeedReset ) )
                    )
                )
            )
            ScriptFiles ( ScriptFile ( Main.sig ) )
            """));

        var config = new SignalConfigurationReader().Read(store, "ROUTES/RouteA/sigcfg.dat");

        var type = Assert.Single(config.SignalTypes.Values);
        Assert.Equal("mainsignal", type.Name);
        Assert.Equal("NORMAL", type.Function);
        Assert.Equal("mainscript", type.Script);
        Assert.Equal("MAIN", type.NormalSubtype);
        Assert.Equal(2, type.NumberClearAhead);
        Assert.True(type.Flags.Contains("abs"));
        Assert.Equal(1, type.Lights.Count);
        Assert.Equal(0.25f, type.Lights[0].Radius, 4);
        var state = Assert.Single(type.DrawStates.Values);
        Assert.True(state.Lights[0].Flashing);
        var aspect = Assert.Single(type.Aspects);
        Assert.Equal("STOP", aspect.Aspect);
        Assert.Equal("stop", aspect.DrawState);
        Assert.True(aspect.Asap);
        Assert.True(aspect.ResetSpeed);
        Assert.Equal("Main.sig", Assert.Single(config.ScriptFiles));
        Assert.Equal("DISTANCE", config.SignalFunctions["DISTANT"]);
        Assert.Empty(config.Diagnostics);
    }

    [Fact]
    public void ReportsMissingDrawStateAndLightReferences()
    {
        using var store = CreateStore(("ROUTES/RouteA/sigcfg.dat", """
            SIMISA@@@@@@@@@@JINX0t1t______
            SignalTypes ( 1
                SignalType ( Broken
                    SignalLights ( 0 )
                    SignalDrawStates ( 1 SignalDrawState ( 0 Ready DrawLights ( 1 DrawLight ( 4 ) ) ) )
                    SignalAspects ( 1 SignalAspect ( CLEAR_1 Missing ) )
                )
            )
            """));

        var config = new SignalConfigurationReader().Read(store, "ROUTES/RouteA/sigcfg.dat");
        Assert.Contains(config.Diagnostics, item => item.Contains("missing draw state"));
        Assert.Contains(config.Diagnostics, item => item.Contains("missing light 4"));
    }

    private static ZipContentStore CreateStore(params (string Path, string Content)[] files)
    {
        var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                var entry = archive.CreateEntry(file.Path);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(file.Content);
            }
        }
        memory.Position = 0;
        return new ZipContentStore(memory);
    }
}
