/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_USE_MOCK_ACTIVITY?: string;
  readonly VITE_WEB_ADOFAI_EMBED_URL?: string;
  readonly VITE_TUF_WEB_URL?: string;
  readonly VITE_TUFREPLAY_BUILD_FLAVOR?: string;
  readonly VITE_TUFREPLAY_ENVIRONMENT?: string;
  readonly VITE_TUFREPLAY_BUILD_SHA?: string;
}
