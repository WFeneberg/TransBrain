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
    DocumentType,

    /// <summary>
    /// Customer class ("Kundenklasse") a customer is assigned to. Not a vehicle list: it lives in this
    /// catalog so customers share its maintenance, deactivation and usage counting.
    /// </summary>
    CustomerClass
}
