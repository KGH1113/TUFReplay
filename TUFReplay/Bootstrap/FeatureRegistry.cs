using JALib.Core;
using TUFReplay.Features.Recording;
using TUFReplay.Features.Replay;

namespace TUFReplay.Bootstrap;

public static class FeatureRegistry
{
  public static Feature[] CreateFeatures()
  {
    return new Feature[]
    {
      new RecordingFeature(),
      new ReplayFeature()
    };
  }
}
