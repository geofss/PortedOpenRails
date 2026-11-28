// Behavior ported from the Open Rails project source.
// Copyright (C) Open Rails contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace OpenRails.Web.Simulation;

/// <summary>
/// Port of Orts.Simulation.Signalling.DeadlockInfo.SelectPath. It preserves
/// Open Rails' own-path/main-path preference and train-length-fit selection.
/// Availability is supplied by the track-circuit reservation layer.
/// </summary>
public sealed class NativeDeadlockPathSelector
{
    public NativeDeadlockPathSelection SelectPath(
        IReadOnlyList<NativeDeadlockPathCandidate> availableRoutes,
        NativeDeadlockPathCandidate ownPath,
        bool deadlockActive)
    {
        ArgumentNullException.ThrowIfNull(availableRoutes);
        ArgumentNullException.ThrowIfNull(ownPath);
        if (availableRoutes.Any(path => path is null || path.PathIndex < 0 ||
                string.IsNullOrWhiteSpace(path.Name)))
            throw new ArgumentException("Available deadlock paths must have valid identities.", nameof(availableRoutes));
        if (availableRoutes.Select(path => path.PathIndex).Distinct().Count() != availableRoutes.Count)
            throw new ArgumentException("Available deadlock path indexes must be unique.", nameof(availableRoutes));
        if (ownPath.PathIndex < 0 || string.IsNullOrWhiteSpace(ownPath.Name))
            throw new ArgumentException("The train's own deadlock path must be valid.", nameof(ownPath));

        var selectedPathNoFit = -1;
        var selectedPathFit = -1;
        var checkedMain = deadlockActive;
        var checkedOwn = ownPath.Name.Equals("MAIN", StringComparison.Ordinal);
        var preferMain = !deadlockActive;
        var defaultPath = ownPath.PathIndex;

        foreach (var path in availableRoutes)
        {
            if (!checkedOwn && path.PathIndex == ownPath.PathIndex)
            {
                checkedOwn = true;
                if (path.TrainFits)
                {
                    selectedPathFit = path.PathIndex;
                    break;
                }

                selectedPathNoFit = path.PathIndex;
                if (checkedMain && selectedPathFit > 0)
                    break;
            }

            if (path.Name.Equals("MAIN", StringComparison.Ordinal))
            {
                checkedMain = true;
                if (path.TrainFits)
                {
                    selectedPathFit = path.PathIndex;
                    if (checkedOwn && preferMain)
                        break;
                }
                else if (!checkedOwn || selectedPathNoFit < 0 || preferMain)
                {
                    selectedPathNoFit = path.PathIndex;
                }
            }
            else if (path.TrainFits)
            {
                selectedPathFit = path.PathIndex;
                if (checkedMain || checkedOwn)
                    break;
            }
            else if ((!checkedOwn && !checkedMain) || !preferMain)
            {
                selectedPathNoFit = path.PathIndex;
            }
        }

        var selected = selectedPathFit >= 0 ? selectedPathFit :
            selectedPathNoFit >= 0 ? selectedPathNoFit : defaultPath;
        var selectedCandidate = availableRoutes.FirstOrDefault(path => path.PathIndex == selected)
            ?? (ownPath.PathIndex == selected ? ownPath : null);
        return new NativeDeadlockPathSelection(selected,
            selectedCandidate?.EndSectionIndex ?? ownPath.EndSectionIndex,
            selectedPathFit >= 0,
            selectedCandidate?.Name ?? ownPath.Name);
    }
}

public sealed record NativeDeadlockPathCandidate(
    int PathIndex, string Name, bool TrainFits, int EndSectionIndex);

public sealed record NativeDeadlockPathSelection(
    int PathIndex, int EndSectionIndex, bool TrainFits, string PathName);
