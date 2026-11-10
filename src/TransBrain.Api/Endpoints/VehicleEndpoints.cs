using TransBrain.Api.Authorization;
using TransBrain.Api.Common;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Vehicles;
using TransBrain.Application.Features.Vehicles.ChangeVehicleStatus;
using TransBrain.Application.Features.Vehicles.CreateVehicle;
using TransBrain.Application.Features.Vehicles.DeleteVehicle;
using TransBrain.Application.Features.Vehicles.GetVehicleById;
using TransBrain.Application.Features.Vehicles.ListVehicles;
using TransBrain.Application.Features.Vehicles.UpdateVehicle;
using TransBrain.Application.Features.Vehicles.VehicleDocuments;
using TransBrain.Application.Features.Vehicles.VehicleDocuments.AddVehicleDocument;
using TransBrain.Application.Features.Vehicles.VehicleDocuments.DeleteVehicleDocument;
using TransBrain.Application.Features.Vehicles.VehicleDocuments.ListVehicleDocuments;
using TransBrain.Application.Features.Vehicles.VehicleDocuments.UpdateVehicleDocument;
using TransBrain.Domain.Common;

namespace TransBrain.Api.Endpoints;

public sealed class VehicleEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/vehicles").WithTags("Vehicles");

        group.MapPost("/", async (
                CreateVehicleCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                Result<VehicleResponse> result = await sender.Send(command, cancellationToken);
                return result.ToHttpResult(vehicle => Results.Created($"/api/vehicles/{vehicle.Id}", vehicle));
            })
            .WithName("CreateVehicle")
            .Produces<VehicleResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(Policies.MasterDataWrite);

        group.MapGet("/", async (
                ISender sender,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 20,
                string? status = null,
                string? type = null) =>
            {
                Result<PagedResult<VehicleResponse>> result =
                    await sender.Send(new ListVehiclesQuery(page, pageSize, status, type), cancellationToken);
                return result.ToHttpResult();
            })
            .WithName("ListVehicles")
            .Produces<PagedResult<VehicleResponse>>()
            .ProducesValidationProblem()
            .RequireAuthorization(Policies.Read);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                Result<VehicleResponse> result = await sender.Send(new GetVehicleByIdQuery(id), cancellationToken);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("GetVehicleById")
            .Produces<VehicleResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
                Guid id, UpdateVehicleRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                Result<VehicleResponse> result = await sender.Send(request.ToCommand(id), cancellationToken);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.MasterDataWrite)
            .WithName("UpdateVehicle")
            .Produces<VehicleResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/status", async (
                Guid id, ChangeVehicleStatusRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                Result<VehicleResponse> result = await sender.Send(
                    new ChangeVehicleStatusCommand(id, request.Status), cancellationToken);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.MasterDataWrite)
            .WithName("ChangeVehicleStatus")
            .WithSummary("Sets a vehicle to Available, InWorkshop or Decommissioned.")
            .WithDescription("A decommissioned vehicle cannot be returned to service (409).")
            .Produces<VehicleResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                Result<Unit> result = await sender.Send(new DeleteVehicleCommand(id), cancellationToken);
                return result.ToHttpResult(_ => Results.NoContent());
            })
            .RequireAuthorization(Policies.MasterDataWrite)
            .WithName("DeleteVehicle")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        MapDocuments(group);
    }

    private static void MapDocuments(RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}/documents", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                Result<VehicleDocumentResponse[]> result =
                    await sender.Send(new ListVehicleDocumentsQuery(id), cancellationToken);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("ListVehicleDocuments")
            .WithSummary("Lists the vehicle's special documents, soonest expiry first.")
            .Produces<VehicleDocumentResponse[]>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/documents", async (
                Guid id, VehicleDocumentRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                Result<VehicleDocumentResponse> result = await sender.Send(
                    new AddVehicleDocumentCommand(
                        id, request.DocumentType, request.LastCheckedOn, request.ValidUntil, request.Remark),
                    cancellationToken);
                return result.ToHttpResult(document =>
                    Results.Created($"/api/vehicles/{id}/documents/{document.Id}", document));
            })
            .RequireAuthorization(Policies.MasterDataWrite)
            .WithName("AddVehicleDocument")
            .WithDescription("A vehicle can hold each document type only once (409).")
            .Produces<VehicleDocumentResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}/documents/{documentId:guid}", async (
                Guid id,
                Guid documentId,
                VehicleDocumentRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                Result<VehicleDocumentResponse> result = await sender.Send(
                    new UpdateVehicleDocumentCommand(
                        id, documentId, request.DocumentType, request.LastCheckedOn, request.ValidUntil, request.Remark),
                    cancellationToken);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.MasterDataWrite)
            .WithName("UpdateVehicleDocument")
            .Produces<VehicleDocumentResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}/documents/{documentId:guid}", async (
                Guid id, Guid documentId, ISender sender, CancellationToken cancellationToken) =>
            {
                Result<Unit> result = await sender.Send(
                    new DeleteVehicleDocumentCommand(id, documentId), cancellationToken);
                return result.ToHttpResult(_ => Results.NoContent());
            })
            .RequireAuthorization(Policies.MasterDataWrite)
            .WithName("DeleteVehicleDocument")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

/// <summary>Body of a vehicle update. The id comes from the route, not the payload.</summary>
public sealed record UpdateVehicleRequest(
    string LicensePlate,
    string Type,
    int PayloadKg,
    decimal LoadMeters,
    DateOnly NextInspectionDue,
    bool IsLicensePlateConfidential = false,
    string? Make = null,
    string? Model = null,
    DateOnly? ManufacturedOn = null,
    int? PowerKw = null,
    string? VehicleClass = null,
    string? CarrierGroup = null,
    Guid? DefaultDriverId = null,
    string? ChassisNumber = null,
    string? EngineNumber = null,
    int? LengthMm = null,
    int? WidthMm = null,
    int? HeightMm = null,
    int? CurbWeightKg = null,
    int? GrossWeightKg = null,
    int? MaxFifthWheelLoadKg = null,
    string? Notes = null,
    VehicleEquipmentData? Equipment = null,
    string? DataSheet = null,
    VehicleCommercialData? Commercial = null)
{
    public UpdateVehicleCommand ToCommand(Guid id) => new(
        id,
        LicensePlate,
        Type,
        PayloadKg,
        LoadMeters,
        NextInspectionDue,
        IsLicensePlateConfidential,
        Make,
        Model,
        ManufacturedOn,
        PowerKw,
        VehicleClass,
        CarrierGroup,
        DefaultDriverId,
        ChassisNumber,
        EngineNumber,
        LengthMm,
        WidthMm,
        HeightMm,
        CurbWeightKg,
        GrossWeightKg,
        MaxFifthWheelLoadKg,
        Notes,
        Equipment,
        DataSheet,
        Commercial);
}

/// <summary>Body of adding or updating a special document. Ids come from the route.</summary>
/// <param name="DocumentType">Code of an active document type catalog entry (case-insensitive).</param>
public sealed record VehicleDocumentRequest(
    string DocumentType,
    DateOnly? LastCheckedOn = null,
    DateOnly? ValidUntil = null,
    string? Remark = null);

/// <summary>Body of a vehicle status change.</summary>
/// <param name="Status">Available, InWorkshop or Decommissioned (case-insensitive).</param>
public sealed record ChangeVehicleStatusRequest(string Status);
