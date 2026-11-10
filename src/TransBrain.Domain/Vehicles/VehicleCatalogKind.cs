namespace TransBrain.Domain.Vehicles;

/// <summary>The maintainable selection lists ("Stammdaten-Listen") a vehicle refers to.</summary>
public enum VehicleCatalogKind
{
    /// <summary>Body/usage class of the vehicle ("Fahrzeugklasse"), for example refrigerated box or tanker.</summary>
    VehicleClass,

    /// <summary>Subcontractor group ("Frächtergruppe") the vehicle is run under.</summary>
    CarrierGroup,

    /// <summary>Insurance company ("Versicherungsgesellschaft") insuring the vehicle or its loads.</summary>
    InsuranceCompany,

    /// <summary>Kind of special document or periodic inspection ("Sonderpapier"), for example the ADR certificate.</summary>
    DocumentType
}
