namespace TransBrain.Domain.Vehicles;

public enum VehicleType
{
    /// <summary>Tractor unit ("Zugmaschine").</summary>
    Tractor,

    /// <summary>Semi-trailer ("Hänger (Sattel)"); it has no engine and cannot drive a tour on its own.</summary>
    SemiTrailer
}
