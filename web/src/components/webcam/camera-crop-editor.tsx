import { Camera01Icon, RotateLeft01Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { type KeyboardEvent, type PointerEvent, useEffect, useId, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useWebcamPreview } from "@/hooks/webcam/use-webcam-preview";
import {
  type CameraCropHandle,
  cameraCropEqual,
  changeCameraCrop,
  displayCameraCrop,
  FULL_CAMERA_CROP,
} from "@/models/webcam/camera-crop";
import type { CameraCrop, WebcamSettingsPatch, WebcamState } from "@/models/webcam/webcam-model";
import { Button } from "@/shared/ui/button";

type CropGesture = {
  pointerId: number;
  target: HTMLButtonElement;
  handle: CameraCropHandle;
  x: number;
  y: number;
  bounds: DOMRect;
  crop: CameraCrop;
  mirrored: boolean;
};

export function CameraCropEditor({
  state,
  active,
  saving,
  onUpdate,
}: {
  state: WebcamState;
  active: boolean;
  saving: boolean;
  onUpdate: (patch: WebcamSettingsPatch) => void;
}) {
  const { t } = useTranslation("webcam");
  const hintId = useId();
  const surface = useRef<HTMLDivElement>(null);
  const gesture = useRef<CropGesture | null>(null);
  const preview = useWebcamPreview(
    active && state.enabled && state.supported,
    state.deviceId,
    state.deviceId
      ? (state.devices.find((device) => device.id === state.deviceId)?.name ?? null)
      : null,
  );
  const [draft, setDraft] = useState(state.crop);
  const draftRef = useRef(draft);
  const [dragging, setDragging] = useState<CameraCropHandle | null>(null);
  const available = preview.available;
  const disabled = saving || !available;
  const display = displayCameraCrop(draft, state.mirror);

  const updateDraft = (crop: CameraCrop) => {
    draftRef.current = crop;
    setDraft(crop);
  };
  const releaseGesture = () => {
    const current = gesture.current;
    gesture.current = null;
    if (current?.target.hasPointerCapture(current.pointerId)) {
      current.target.releasePointerCapture(current.pointerId);
    }
    setDragging(null);
  };
  const cancelGesture = () => {
    releaseGesture();
    updateDraft(state.crop);
  };

  useEffect(() => {
    if (!gesture.current && !saving) {
      draftRef.current = state.crop;
      setDraft(state.crop);
    }
  }, [state.crop, saving]);

  useEffect(() => {
    const current = gesture.current;
    if (!current || (available && current.mirrored === state.mirror)) return;
    gesture.current = null;
    if (current.target.hasPointerCapture(current.pointerId)) {
      current.target.releasePointerCapture(current.pointerId);
    }
    setDragging(null);
    draftRef.current = state.crop;
    setDraft(state.crop);
  }, [available, state.mirror, state.crop]);

  useEffect(() => {
    const element = surface.current;
    if (!element) return;
    const observer = new ResizeObserver(() => {
      const current = gesture.current;
      if (!current) return;
      const bounds = element.getBoundingClientRect();
      if (
        Math.abs(bounds.width - current.bounds.width) > 1 ||
        Math.abs(bounds.height - current.bounds.height) > 1
      ) {
        gesture.current = null;
        if (current.target.hasPointerCapture(current.pointerId)) {
          current.target.releasePointerCapture(current.pointerId);
        }
        setDragging(null);
        draftRef.current = displayCameraCrop(current.crop, current.mirrored);
        setDraft(draftRef.current);
      }
    });
    observer.observe(element);
    return () => {
      observer.disconnect();
      const current = gesture.current;
      gesture.current = null;
      if (current?.target.hasPointerCapture(current.pointerId)) {
        current.target.releasePointerCapture(current.pointerId);
      }
    };
  }, []);

  const begin = (event: PointerEvent<HTMLButtonElement>, handle: CameraCropHandle) => {
    if (disabled || event.button !== 0 || gesture.current || !surface.current) return;
    event.preventDefault();
    const bounds = surface.current.getBoundingClientRect();
    if (bounds.width <= 0 || bounds.height <= 0) return;
    event.currentTarget.focus();
    event.currentTarget.setPointerCapture(event.pointerId);
    gesture.current = {
      pointerId: event.pointerId,
      target: event.currentTarget,
      handle,
      x: event.clientX,
      y: event.clientY,
      bounds,
      crop: displayCameraCrop(draftRef.current, state.mirror),
      mirrored: state.mirror,
    };
    setDragging(handle);
  };
  const move = (event: PointerEvent<HTMLButtonElement>) => {
    const current = gesture.current;
    if (!current || current.pointerId !== event.pointerId) return;
    const next = changeCameraCrop(
      current.crop,
      current.handle,
      (event.clientX - current.x) / current.bounds.width,
      (event.clientY - current.y) / current.bounds.height,
    );
    updateDraft(displayCameraCrop(next, current.mirrored));
  };
  const finish = (event: PointerEvent<HTMLButtonElement>) => {
    if (gesture.current?.pointerId !== event.pointerId) return;
    move(event);
    releaseGesture();
    if (!cameraCropEqual(draftRef.current, state.crop)) onUpdate({ crop: draftRef.current });
  };
  const keyboard = (event: KeyboardEvent<HTMLButtonElement>, handle: CameraCropHandle) => {
    if (event.key === "Escape" && gesture.current) {
      event.preventDefault();
      event.stopPropagation();
      cancelGesture();
      return;
    }
    if (disabled || gesture.current) return;
    const step = event.shiftKey ? 0.02 : 0.005;
    const directions: Record<string, [number, number]> = {
      ArrowLeft: [-step, 0],
      ArrowRight: [step, 0],
      ArrowUp: [0, -step],
      ArrowDown: [0, step],
    };
    const delta = directions[event.key];
    if (!delta) return;
    event.preventDefault();
    const next = displayCameraCrop(
      changeCameraCrop(displayCameraCrop(draftRef.current, state.mirror), handle, ...delta),
      state.mirror,
    );
    if (cameraCropEqual(next, state.crop)) return;
    updateDraft(next);
    onUpdate({ crop: next });
  };
  const pointerHandlers = {
    onPointerMove: move,
    onPointerUp: finish,
    onPointerCancel: cancelGesture,
    onLostPointerCapture: () => {
      if (gesture.current) cancelGesture();
    },
  };

  return (
    <section className="space-y-3" aria-label={t("cropTitle")}>
      <div className="flex items-center justify-between gap-2">
        <h3 className="text-sm font-semibold">{t("cropTitle")}</h3>
        <div className="flex items-center gap-1">
          {preview.revealed ? (
            <Button size="xs" variant="ghost" onClick={preview.hide}>
              {t("cropPreview.hide")}
            </Button>
          ) : null}
          <Button
            size="xs"
            variant="ghost"
            disabled={saving || cameraCropEqual(draft, FULL_CAMERA_CROP)}
            onClick={() => {
              cancelGesture();
              updateDraft(FULL_CAMERA_CROP);
              onUpdate({ crop: FULL_CAMERA_CROP });
            }}
          >
            <HugeiconsIcon aria-hidden="true" icon={RotateLeft01Icon} size={13} />
            {t("resetCrop")}
          </Button>
        </div>
      </div>
      <div className="px-3 py-3">
        <div
          ref={surface}
          data-camera-pixel-plane=""
          className="relative isolate mx-auto w-full select-none bg-black"
          style={{
            marginBlock: 0,
            padding: 0,
            border: 0,
            borderRadius: 0,
            maxWidth: `${(preview.width && preview.height ? preview.width / preview.height : 16 / 9) * 24}rem`,
            aspectRatio:
              preview.width && preview.height ? `${preview.width} / ${preview.height}` : "16 / 9",
          }}
        >
          <div
            className="pointer-events-none absolute inset-0 overflow-hidden"
            style={{ borderRadius: 0 }}
          >
            {preview.revealed ? (
              <video
                ref={preview.videoRef}
                aria-label={t("cropPreview.image")}
                autoPlay
                muted
                playsInline
                className="absolute inset-0 size-full object-contain"
                style={{ borderRadius: 0, transform: state.mirror ? "scaleX(-1)" : undefined }}
                onLoadedMetadata={preview.updateSize}
                onLoadedData={preview.updateSize}
                onResize={preview.updateSize}
                onError={preview.fail}
              />
            ) : null}
            {available ? (
              <div
                aria-hidden="true"
                className="absolute outline-2 -outline-offset-2 outline-white/90"
                style={{
                  left: `${display.x * 100}%`,
                  top: `${display.y * 100}%`,
                  width: `${display.width * 100}%`,
                  height: `${display.height * 100}%`,
                  boxShadow: "0 0 0 9999px rgb(0 0 0 / 65%)",
                }}
              />
            ) : null}
          </div>
          {available ? (
            <>
              <button
                type="button"
                aria-label={t("moveCrop")}
                aria-describedby={hintId}
                disabled={disabled}
                className="absolute touch-none bg-transparent outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary disabled:cursor-default"
                style={{
                  margin: 0,
                  padding: 0,
                  border: 0,
                  left: `${display.x * 100}%`,
                  top: `${display.y * 100}%`,
                  width: `${display.width * 100}%`,
                  height: `${display.height * 100}%`,
                  cursor: dragging === "move" ? "grabbing" : "grab",
                }}
                onPointerDown={(event) => begin(event, "move")}
                onKeyDown={(event) => keyboard(event, "move")}
                {...pointerHandlers}
              />
              {(["nw", "ne", "sw", "se"] as const).map((handle) => (
                <button
                  key={handle}
                  type="button"
                  data-camera-crop-handle={handle}
                  aria-label={t(`cropHandles.${handle}`)}
                  aria-describedby={hintId}
                  disabled={disabled}
                  className="absolute z-10 grid size-12 touch-none place-items-center outline-none focus-visible:bg-primary/30 disabled:cursor-default"
                  style={{
                    margin: 0,
                    padding: 0,
                    border: 0,
                    boxSizing: "border-box",
                    transform: "translate(-50%, -50%)",
                    left: `${(display.x + (handle.endsWith("e") ? display.width : 0)) * 100}%`,
                    top: `${(display.y + (handle.startsWith("s") ? display.height : 0)) * 100}%`,
                    cursor: handle === "nw" || handle === "se" ? "nwse-resize" : "nesw-resize",
                  }}
                  onPointerDown={(event) => begin(event, handle)}
                  onKeyDown={(event) => keyboard(event, handle)}
                  {...pointerHandlers}
                >
                  <span
                    aria-hidden="true"
                    className="size-3 rounded-full border border-black/30 bg-white shadow-sm"
                  />
                </button>
              ))}
            </>
          ) : (
            <div className="absolute inset-0 flex flex-col items-center justify-center gap-3 px-5 text-center text-sm text-white/90">
              <HugeiconsIcon aria-hidden="true" icon={Camera01Icon} size={26} strokeWidth={1.6} />
              {state.enabled && state.supported && active ? (
                !preview.revealed ? (
                  <>
                    <Button variant="outline" onClick={preview.reveal}>
                      {t("cropPreview.reveal")}
                    </Button>
                    <p className="max-w-72 text-xs leading-relaxed text-white/60">
                      {t("cropPreview.privacy")}
                    </p>
                  </>
                ) : preview.selectionRequired ? (
                  <div className="w-full max-w-72 space-y-2 text-left">
                    <label htmlFor={`${hintId}-device`} className="text-xs font-medium">
                      {t("cropPreview.device")}
                    </label>
                    <select
                      id={`${hintId}-device`}
                      value={preview.selectedDeviceId}
                      onChange={(event) => preview.selectDevice(event.target.value)}
                      className="h-9 w-full rounded-lg border border-white/30 bg-black px-2 text-sm text-white outline-none focus-visible:ring-2 focus-visible:ring-primary"
                    >
                      <option value="">{t("cropPreview.chooseDevice")}</option>
                      {preview.devices.map((device, index) => (
                        <option key={device.id} value={device.id}>
                          {device.label || t("cropPreview.unnamedDevice", { count: index + 1 })}
                        </option>
                      ))}
                    </select>
                    <p className="text-xs leading-relaxed text-white/70">
                      {t("cropPreview.deviceHint")}
                    </p>
                  </div>
                ) : preview.error ? (
                  <>
                    <p role="alert" className="max-w-80 text-xs leading-relaxed">
                      {preview.error}
                    </p>
                    <Button size="sm" variant="outline" onClick={preview.retry}>
                      {t("cropPreview.retry")}
                    </Button>
                  </>
                ) : (
                  <p role="status" className="max-w-80 text-xs leading-relaxed text-white/70">
                    {t("cropPreview.waiting")}
                  </p>
                )
              ) : (
                <p className="max-w-80 text-xs leading-relaxed text-white/70">
                  {active ? t("cropPreview.off") : t("cropPreview.disconnected")}
                </p>
              )}
            </div>
          )}
        </div>
      </div>
      {available && preview.camera?.label ? (
        <p className="text-xs text-muted-foreground">{preview.camera.label}</p>
      ) : null}
      <p id={hintId} className="text-xs leading-relaxed text-muted-foreground">
        {t("cropHint")}
      </p>
      <p className="text-xs leading-relaxed text-muted-foreground">{t("cropRecordingHint")}</p>
    </section>
  );
}
