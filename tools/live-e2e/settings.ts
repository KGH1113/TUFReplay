import { installSettings, restoreSettings } from "./game-settings";

switch (process.argv[2]) {
	case "install":
		await installSettings();
		break;
	case "restore":
		await restoreSettings();
		break;
	default:
		throw new Error("Usage: bun tools/live-e2e/settings.ts install|restore");
}
