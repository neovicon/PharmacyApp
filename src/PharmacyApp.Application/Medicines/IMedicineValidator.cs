using PharmacyApp.Application.Common;
using PharmacyApp.Application.Medicines.Commands;

namespace PharmacyApp.Application.Medicines;

/// <summary>
/// Contract for validating medicine command inputs against business rules and sensible constraints.
/// </summary>
public interface IMedicineValidator
{
    Result ValidateCreate(CreateMedicineCommand command);
    Result ValidateUpdate(UpdateMedicineCommand command);
}
