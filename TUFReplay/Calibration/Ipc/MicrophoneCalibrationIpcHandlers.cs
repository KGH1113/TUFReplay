using TUFReplay.Calibration.Ipc;
using TUFReplay.Calibration.Models;
using TUFReplay.Composition;
using TUFReplay.Shared.Ipc;

namespace TUFReplay.Calibration.Ipc;

public static class MicrophoneCalibrationIpcHandlers
{
  public static MicrophoneCalibrationStatusDto Start() =>
    MicrophoneCalibrationStatusDto.From(FeatureRegistry.MicrophoneCalibration.Start());

  public static MicrophoneCalibrationStatusDto GetStatus(string operationId)
  {
    if (string.IsNullOrWhiteSpace(operationId))
      throw IpcDomainError.Create("invalid_operation_id", "operationId must be a non-empty string.");
    if (!FeatureRegistry.MicrophoneCalibration.IsCurrentOperation(operationId))
      throw StaleOperationError();
    return MicrophoneCalibrationStatusDto.From(FeatureRegistry.MicrophoneCalibration.GetStatus());
  }

  public static MicrophoneCalibrationResultDto GetResult(string operationId, int revision = 0)
  {
    if (string.IsNullOrWhiteSpace(operationId))
      throw IpcDomainError.Create("invalid_operation_id", "operationId must be a non-empty string.");
    if (revision < 0)
      throw IpcDomainError.Create("invalid_result_revision", "revision must be zero or a positive integer.");
    MicrophoneCalibrationResult result = FeatureRegistry.MicrophoneCalibration.GetResult(operationId, revision);
    return result == null
      ? throw IpcDomainError.Create("calibration_result_unavailable", "The calibration result is not available.")
      : MicrophoneCalibrationResultDto.From(result);
  }

  public static MicrophoneCalibrationStatusDto PlayPreview(string operationId)
  {
    if (string.IsNullOrWhiteSpace(operationId))
      throw IpcDomainError.Create("invalid_operation_id", "operationId must be a non-empty string.");
    if (!FeatureRegistry.MicrophoneCalibration.IsCurrentOperation(operationId))
      throw StaleOperationError();
    return MicrophoneCalibrationStatusDto.From(FeatureRegistry.MicrophoneCalibration.PlayPreview(operationId));
  }

  public static MicrophoneCalibrationStatusDto StopPreview(string operationId)
  {
    if (string.IsNullOrWhiteSpace(operationId))
      throw IpcDomainError.Create("invalid_operation_id", "operationId must be a non-empty string.");
    if (!FeatureRegistry.MicrophoneCalibration.IsCurrentOperation(operationId))
      throw StaleOperationError();
    return MicrophoneCalibrationStatusDto.From(FeatureRegistry.MicrophoneCalibration.StopPreview());
  }

  public static MicrophoneCalibrationStatusDto SetOffset(string operationId, int offsetMs)
  {
    if (string.IsNullOrWhiteSpace(operationId))
      throw IpcDomainError.Create("invalid_operation_id", "operationId must be a non-empty string.");
    if (!FeatureRegistry.MicrophoneCalibration.IsCurrentOperation(operationId))
      throw StaleOperationError();
    return MicrophoneCalibrationStatusDto.From(FeatureRegistry.MicrophoneCalibration.SetOffset(operationId, offsetMs));
  }

  public static MicrophoneCalibrationStatusDto SetVolume(string operationId, int volumeDb)
  {
    if (string.IsNullOrWhiteSpace(operationId))
      throw IpcDomainError.Create("invalid_operation_id", "operationId must be a non-empty string.");
    if (!FeatureRegistry.MicrophoneCalibration.IsCurrentOperation(operationId))
      throw StaleOperationError();
    return MicrophoneCalibrationStatusDto.From(FeatureRegistry.MicrophoneCalibration.SetVolume(operationId, volumeDb));
  }

  public static MicrophoneCalibrationStatusDto Close(string operationId)
  {
    if (string.IsNullOrWhiteSpace(operationId))
      throw IpcDomainError.Create("invalid_operation_id", "operationId must be a non-empty string.");
    if (!FeatureRegistry.MicrophoneCalibration.IsCurrentOperation(operationId))
      throw StaleOperationError();
    return MicrophoneCalibrationStatusDto.From(FeatureRegistry.MicrophoneCalibration.Close(operationId));
  }

  private static AdofaiIpc.IpcCallException StaleOperationError() =>
    IpcDomainError.Create("calibration_operation_stale", "The calibration operation is no longer active.");
}
