using AdofaiIpc.Core;
using TUFReplay.Application.Calibration;
using TUFReplay.Bootstrap;
using TUFReplay.Ipc.Dtos;

namespace TUFReplay.Features.Ipc;

public static class MicrophoneCalibrationIpcHandlers
{
  public static object Start(IpcRequest request) =>
    MicrophoneCalibrationStatusDto.From(FeatureRegistry.MicrophoneCalibration.Start());

  public static object GetStatus(IpcRequest request)
  {
    if (!TryOperationId(request, out string operationId, out object error))
      return error;
    if (!FeatureRegistry.MicrophoneCalibration.IsCurrentOperation(operationId))
      return StaleOperationError();
    return MicrophoneCalibrationStatusDto.From(FeatureRegistry.MicrophoneCalibration.GetStatus());
  }

  public static object GetResult(IpcRequest request)
  {
    if (!TryOperationId(request, out string operationId, out object error))
      return error;
    int revision = IpcParams.OptionalInt(request, "revision") ?? 0;
    if (revision < 0)
      return IpcDomainError.Create("invalid_result_revision", "revision must be zero or a positive integer.");
    MicrophoneCalibrationResult result = FeatureRegistry.MicrophoneCalibration.GetResult(operationId, revision);
    return result == null
      ? IpcDomainError.Create("calibration_result_unavailable", "The calibration result is not available.")
      : MicrophoneCalibrationResultDto.From(result);
  }

  public static object PlayPreview(IpcRequest request)
  {
    if (!TryOperationId(request, out string operationId, out object error))
      return error;
    if (!FeatureRegistry.MicrophoneCalibration.IsCurrentOperation(operationId))
      return StaleOperationError();
    return MicrophoneCalibrationStatusDto.From(FeatureRegistry.MicrophoneCalibration.PlayPreview(operationId));
  }

  public static object StopPreview(IpcRequest request)
  {
    if (!TryOperationId(request, out string operationId, out object error))
      return error;
    if (!FeatureRegistry.MicrophoneCalibration.IsCurrentOperation(operationId))
      return StaleOperationError();
    return MicrophoneCalibrationStatusDto.From(FeatureRegistry.MicrophoneCalibration.StopPreview());
  }

  public static object SetOffset(IpcRequest request)
  {
    if (!TryOperationId(request, out string operationId, out object error))
      return error;
    int? offsetMs = IpcParams.OptionalInt(request, "offsetMs");
    if (!offsetMs.HasValue)
      return IpcDomainError.Create("invalid_microphone_offset", "offsetMs must be an integer.");
    if (!FeatureRegistry.MicrophoneCalibration.IsCurrentOperation(operationId))
      return StaleOperationError();
    return MicrophoneCalibrationStatusDto.From(
      FeatureRegistry.MicrophoneCalibration.SetOffset(operationId, offsetMs.Value)
    );
  }

  public static object SetVolume(IpcRequest request)
  {
    if (!TryOperationId(request, out string operationId, out object error))
      return error;
    int? volumeDb = IpcParams.OptionalInt(request, "volumeDb");
    if (!volumeDb.HasValue)
      return IpcDomainError.Create("invalid_microphone_volume", "volumeDb must be an integer.");
    if (!FeatureRegistry.MicrophoneCalibration.IsCurrentOperation(operationId))
      return StaleOperationError();
    return MicrophoneCalibrationStatusDto.From(
      FeatureRegistry.MicrophoneCalibration.SetVolume(operationId, volumeDb.Value)
    );
  }

  public static object Close(IpcRequest request)
  {
    if (!TryOperationId(request, out string operationId, out object error))
      return error;
    if (!FeatureRegistry.MicrophoneCalibration.IsCurrentOperation(operationId))
      return StaleOperationError();
    return MicrophoneCalibrationStatusDto.From(FeatureRegistry.MicrophoneCalibration.Close(operationId));
  }

  private static bool TryOperationId(IpcRequest request, out string operationId, out object error)
  {
    if (IpcParams.TryRequiredString(request, "operationId", out operationId))
    {
      error = null;
      return true;
    }
    error = IpcDomainError.Create("invalid_operation_id", "operationId must be a non-empty string.");
    return false;
  }

  private static object StaleOperationError() =>
    IpcDomainError.Create("calibration_operation_stale", "The calibration operation is no longer active.");
}
