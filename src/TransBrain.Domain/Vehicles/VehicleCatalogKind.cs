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
    CustomerClass,

    /// <summary>
    /// Kind of phone number ("Telefontyp") of a driver's phone list, for example private or car phone.
    /// Not a vehicle list: it lives in this catalog so drivers share its maintenance, deactivation and
    /// usage counting.
    /// </summary>
    PhoneType,

    /// <summary>
    /// Kind of special paper ("Sonderpapier-Art") of a driver's document list, for example a dangerous
    /// goods certificate or a TÜV assessment. Not a vehicle list (that one is
    /// <see cref="DocumentType"/>): it lives in this catalog so drivers share its maintenance,
    /// deactivation and usage counting.
    /// </summary>
    DriverDocumentType,

    /// <summary>
    /// Toll category ("Maut-Art") of an order's toll records, for example the Austrian or German
    /// per-kilometre rate. Not a vehicle list: it lives in this catalog so orders share its
    /// maintenance, deactivation and usage counting.
    /// </summary>
    TollCategory,

    /// <summary>
    /// Measurement unit ("Einheit") of an order's additional charge positions, for example hours,
    /// pallets or litres. Not a vehicle list: it lives in this catalog so orders share its
    /// maintenance, deactivation and usage counting.
    /// </summary>
    Unit
}
