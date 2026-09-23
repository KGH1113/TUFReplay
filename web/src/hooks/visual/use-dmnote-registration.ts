import { type KeyboardEvent, type PointerEvent, useEffect, useRef, useState } from "react";
import {
  clampDmnotePlacement,
  DMNOTE_SCALE_DEFAULT,
  DMNOTE_VIEWPORT_HEIGHT,
  DMNOTE_VIEWPORT_WIDTH,
  type DmnoteLayout,
  type DmnotePlacement,
  dmnoteRegistrationLayout,
  prepareDmnoteImport,
} from "@/models/visual/dmnote-registration-model";

export function useDmnoteRegistration(file: File | null, cssFile: File | null) {
  const [layout, setLayout] = useState<DmnoteLayout | null>(null);
  const [position, setPosition] = useState<DmnotePlacement>({
    x: 0,
    y: 0,
    scale: DMNOTE_SCALE_DEFAULT,
  });
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const [keyCounterEnabled, setKeyCounterEnabled] = useState(false);
  const [json, setJson] = useState<string | null>(null);
  const drag = useRef<{ x: number; y: number; start: DmnotePlacement; scale: number } | null>(null);
  useEffect(() => {
    let cancelled = false;
    setLayout(null);
    setJson(null);
    setError(null);
    setKeyCounterEnabled(false);
    if (!file) {
      setPending(false);
      return;
    }
    setPending(true);
    void (async () => {
      if (file.size > 64 * 1024 * 1024) throw new Error("visual_payload_too_large");
      const source = await file.text();
      const next = dmnoteRegistrationLayout(source);
      if (!cancelled) {
        setJson(source);
        setLayout(next);
        setKeyCounterEnabled(JSON.parse(source).keyCounterEnabled === true);
        setPosition(
          clampDmnotePlacement(
            { x: 32, y: DMNOTE_VIEWPORT_HEIGHT - next.height - 32, scale: DMNOTE_SCALE_DEFAULT },
            next,
          ),
        );
      }
    })()
      .catch((cause) => {
        if (!cancelled) setError(cause instanceof Error ? cause.message : "visual_bundle_invalid");
      })
      .finally(() => {
        if (!cancelled) setPending(false);
      });
    return () => {
      cancelled = true;
    };
  }, [file]);
  const move = (next: DmnotePlacement) => {
    if (layout) setPosition(clampDmnotePlacement(next, layout));
  };
  return {
    layout,
    position,
    error,
    pending,
    move,
    keyCounterEnabled,
    setKeyCounterEnabled,
    async prepare() {
      if (!json || !layout || error) throw new Error(error ?? "visual_bundle_invalid");
      if (cssFile && cssFile.size > 1024 * 1024) throw new Error("visual_payload_too_large");
      return prepareDmnoteImport(
        json,
        cssFile ? await cssFile.text() : undefined,
        position,
        keyCounterEnabled,
      );
    },
    onPointerDown(event: PointerEvent<SVGSVGElement>) {
      event.preventDefault();
      event.currentTarget.setPointerCapture(event.pointerId);
      const rect = event.currentTarget.getBoundingClientRect();
      drag.current = {
        x: event.clientX,
        y: event.clientY,
        start: position,
        scale:
          rect.width > 0 && Number.isFinite(rect.width) ? DMNOTE_VIEWPORT_WIDTH / rect.width : 1,
      };
    },
    onPointerMove(event: PointerEvent<SVGSVGElement>) {
      const current = drag.current;
      if (current)
        move({
          x: current.start.x + (event.clientX - current.x) * current.scale,
          y: current.start.y + (event.clientY - current.y) * current.scale,
          scale: current.start.scale,
        });
    },
    onPointerUp() {
      drag.current = null;
    },
    onKeyDown(event: KeyboardEvent<SVGSVGElement>) {
      const step = event.shiftKey ? 10 : 1;
      const offsets: Record<string, [number, number]> = {
        ArrowLeft: [-step, 0],
        ArrowRight: [step, 0],
        ArrowUp: [0, -step],
        ArrowDown: [0, step],
      };
      const offset = offsets[event.key];
      if (offset) {
        event.preventDefault();
        move({ x: position.x + offset[0], y: position.y + offset[1], scale: position.scale });
      }
    },
  };
}
