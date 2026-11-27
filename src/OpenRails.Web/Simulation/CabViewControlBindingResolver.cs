using OpenRails.Web.Parsing;

namespace OpenRails.Web.Simulation;

/// <summary>Maps only native cab controls with an equivalent browser train signal or command.</summary>
public static class CabViewControlBindingResolver
{
    public static CabViewControlBinding Resolve(CabViewControlDefinition control)
    {
        ArgumentNullException.ThrowIfNull(control);
        var type = control.ControlType.Trim().ToUpperInvariant();
        var lever = control.Kind.Equals("lever", StringComparison.OrdinalIgnoreCase);
        return type switch
        {
            "SPEEDOMETER" => CabViewControlBinding.Speedometer,
            "THROTTLE" or "REGULATOR" => lever ? CabViewControlBinding.ThrottleLever : CabViewControlBinding.ThrottleDisplay,
            "THROTTLE_DISPLAY" => CabViewControlBinding.ThrottleDisplay,
            "TRAIN_BRAKE" => lever ? CabViewControlBinding.TrainBrakeLever : CabViewControlBinding.TrainBrakeDisplay,
            "DIRECTION" => lever ? CabViewControlBinding.DirectionLever : CabViewControlBinding.DirectionDisplay,
            "DIRECTION_DISPLAY" => CabViewControlBinding.DirectionLever,
            "BRAKE_PIPE" => CabViewControlBinding.BrakePipe,
            "BRAKE_CYL" => CabViewControlBinding.BrakeCylinder,
            "DOORS_DISPLAY" => CabViewControlBinding.Doors,
            "EMERGENCY_BRAKE" => CabViewControlBinding.EmergencyBrake,
            _ => CabViewControlBinding.NotConnected
        };
    }
}

public enum CabViewControlBinding
{
    NotConnected, Speedometer, ThrottleLever, ThrottleDisplay, TrainBrakeLever,
    DirectionLever, DirectionDisplay, TrainBrakeDisplay, BrakePipe, BrakeCylinder, Doors, EmergencyBrake
}
