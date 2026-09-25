import type {
  VisualInspection,
  VisualKind,
  VisualPreset,
  VisualPresetPage,
  VisualSource,
  VisualSources,
} from "@/models/visual/visual-model";
import type {
  VisualRegistrationReporter,
  VisualRegistrationResult,
} from "@/models/visual/visual-registration-model";

export interface VisualPresetImport {
  name: string;
  kind: VisualKind;
  source: VisualSource;
  presetJson?: string;
  assets?: Array<{ reference: string; data_base64: string }>;
}

export interface VisualApi {
  listPresets(): Promise<VisualPresetPage>;
  getSources(): Promise<VisualSources>;
  inspectPreset(input: VisualPresetImport): Promise<VisualInspection>;
  importPreset(input: VisualPresetImport): Promise<VisualPreset>;
  registerPreset(
    input: VisualPresetImport,
    onProgress?: VisualRegistrationReporter,
  ): Promise<VisualRegistrationResult>;
  renamePreset(id: string, name: string): Promise<VisualPreset>;
  removePreset(id: string): Promise<void>;
}
