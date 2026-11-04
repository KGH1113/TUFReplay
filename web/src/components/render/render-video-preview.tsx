import { PauseIcon, PlayIcon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { RenderNotice } from "@/components/render/render-notice";
import { formatFileSize } from "@/models/activity/file-size";
import {
  openForPreview,
  type PreviewSkip,
  previewSkipReason,
} from "@/models/render/render-preview";
import type { TransferredFile } from "@/ports/received-file";
import { Button } from "@/shared/ui/button";
import { Slider } from "@/shared/ui/slider";

/**
 * The finished video, played here with the app's own controls so it looks the
 * same in every browser. The file stays on the computer running ADOFAI: the
 * player streams only the parts it shows, so a long 4K video costs no memory.
 * A format this browser cannot play is never loaded at all.
 */
export function RenderVideoPreview({
  jobId,
  readVideo,
}: {
  jobId: string;
  readVideo: (jobId: string) => Promise<TransferredFile>;
}) {
  const { t, i18n } = useTranslation("render");
  const [url, setUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [failed, setFailed] = useState(false);
  const [skip, setSkip] = useState<{ reason: PreviewSkip; size: number } | null>(null);
  const [attempt, setAttempt] = useState(0);
  // Closes the streamed file in the game, or frees the copied one.
  const release = useRef<(() => void) | null>(null);

  // biome-ignore lint/correctness/useExhaustiveDependencies: A retry starts a fresh transfer.
  useEffect(() => {
    const abort = new AbortController();
    const releaseOnce = (free: () => void) => {
      release.current = () => {
        release.current = null;
        free();
      };
    };
    setLoading(true);
    setFailed(false);
    setSkip(null);
    setUrl(null);
    void (async () => {
      try {
        const file = await readVideo(jobId);
        if (abort.signal.aborted) return;
        if (!Number.isSafeInteger(file.size) || file.size < 0)
          throw new Error("Invalid video size.");
        // The game sends nothing until the file is opened or read, so skipping costs nothing.
        if (previewSkipReason(file, canPlayVideo, true) === "format") {
          setSkip({ reason: "format", size: file.size });
          return;
        }
        const opened = await openForPreview(file, abort.signal);
        if (abort.signal.aborted) {
          void opened?.close();
          return;
        }
        if (opened) {
          releaseOnce(() => void opened.close());
          setUrl(opened.url);
          return;
        }
        // An older game cannot stream, so a small enough video is copied instead.
        const reason = previewSkipReason(file, canPlayVideo);
        if (reason) {
          setSkip({ reason, size: file.size });
          return;
        }
        const blob = await new Response(file.stream({ signal: abort.signal }), {
          headers: { "Content-Type": file.type || "application/octet-stream" },
        }).blob();
        if (abort.signal.aborted) return;
        if (blob.size !== file.size) throw new Error("Incomplete video transfer.");
        const objectUrl = URL.createObjectURL(blob);
        releaseOnce(() => URL.revokeObjectURL(objectUrl));
        setUrl(objectUrl);
      } catch {
        if (!abort.signal.aborted) setFailed(true);
      } finally {
        if (!abort.signal.aborted) setLoading(false);
      }
    })();
    return () => {
      abort.abort();
      release.current?.();
    };
  }, [jobId, readVideo, attempt]);

  if (skip)
    return (
      <RenderNotice
        title={
          skip.reason === "size"
            ? t("result.previewTooLarge", { size: formatFileSize(skip.size, i18n.language) })
            : t("result.previewFormat")
        }
        message={t("result.previewElsewhere")}
      />
    );

  return (
    <div className="overflow-hidden rounded-xl border border-border bg-black text-white">
      {url && !failed ? (
        <Player
          url={url}
          onError={() => setFailed(true)}
          // The browser opened the file but cannot decode its picture, such as ProRes in Chrome.
          onNoPicture={() => {
            release.current?.();
            setUrl(null);
            setSkip({ reason: "format", size: 0 });
          }}
        />
      ) : loading ? (
        <div
          role="status"
          className="flex aspect-video items-center justify-center text-sm text-white/70"
        >
          {t("result.previewLoading")}
        </div>
      ) : (
        <div
          role="status"
          className="flex aspect-video flex-col items-center justify-center gap-3 px-6 text-center"
        >
          <p className="max-w-xs text-sm leading-relaxed text-white/80">
            {t("result.previewFailed")}
          </p>
          <Button
            type="button"
            variant="outline"
            size="sm"
            className="border-white/20 bg-transparent text-white hover:bg-white/10 hover:text-white"
            onClick={() => setAttempt((value) => value + 1)}
          >
            {t("result.reload")}
          </Button>
        </div>
      )}
    </div>
  );
}

function canPlayVideo(type: string) {
  return document.createElement("video").canPlayType(type) !== "";
}

function Player({
  url,
  onError,
  onNoPicture,
}: {
  url: string;
  onError: () => void;
  onNoPicture: () => void;
}) {
  const { t } = useTranslation("render");
  const video = useRef<HTMLVideoElement>(null);
  const [playing, setPlaying] = useState(false);
  const [time, setTime] = useState(0);
  const [duration, setDuration] = useState(0);

  // The bar follows the picture every frame while it plays, not four times a second.
  useEffect(() => {
    if (!playing) return;
    let frame = requestAnimationFrame(function follow() {
      if (video.current) setTime(video.current.currentTime);
      frame = requestAnimationFrame(follow);
    });
    return () => cancelAnimationFrame(frame);
  }, [playing]);

  const toggle = () => {
    const element = video.current;
    if (!element) return;
    if (element.paused || element.ended) void element.play().catch(onError);
    else element.pause();
  };

  return (
    <div>
      <div className="relative">
        {/* biome-ignore lint/a11y/useMediaCaption: Rendered gameplay videos have no caption tracks. */}
        <video
          ref={video}
          src={url}
          playsInline
          preload="auto"
          className="aspect-video w-full cursor-pointer bg-black"
          onClick={toggle}
          onPlay={() => setPlaying(true)}
          onPause={() => setPlaying(false)}
          onEnded={() => setPlaying(false)}
          onLoadedMetadata={(event) => {
            if (event.currentTarget.videoWidth === 0) onNoPicture();
          }}
          // Some files report an endless duration until they have loaded.
          onDurationChange={(event) => {
            const length = event.currentTarget.duration;
            setDuration(Number.isFinite(length) ? length : 0);
          }}
          onTimeUpdate={(event) => {
            if (!playing) setTime(event.currentTarget.currentTime);
          }}
          onError={onError}
        />
        {playing ? null : (
          <button
            type="button"
            aria-label={t("result.play")}
            onClick={toggle}
            className="absolute inset-0 m-auto flex size-14 items-center justify-center rounded-full bg-black/55 text-white backdrop-blur-md transition-transform outline-none hover:bg-black/65 focus-visible:ring-3 focus-visible:ring-white/50 active:scale-95"
          >
            <HugeiconsIcon
              aria-hidden="true"
              icon={PlayIcon}
              size={24}
              className="translate-x-0.5"
            />
          </button>
        )}
      </div>
      <div className="flex items-center gap-3 px-3 py-2">
        <Button
          type="button"
          variant="ghost"
          size="icon"
          aria-label={t(playing ? "result.pause" : "result.play")}
          className="size-8 shrink-0 rounded-full text-white hover:bg-white/10 hover:text-white"
          onClick={toggle}
        >
          <HugeiconsIcon aria-hidden="true" icon={playing ? PauseIcon : PlayIcon} size={16} />
        </Button>
        <Slider
          aria-label={t("result.seek")}
          aria-valuetext={`${clock(time)} / ${clock(duration)}`}
          min={0}
          max={duration || 1}
          step={0.01}
          value={[Math.min(time, duration || 1)]}
          disabled={!duration}
          className="flex-1"
          onValueChange={([next]) => {
            setTime(next);
            if (video.current) video.current.currentTime = next;
          }}
        />
        <span className="shrink-0 text-xs tabular-nums text-white/70">
          {clock(time)} / {clock(duration)}
        </span>
      </div>
    </div>
  );
}

function clock(seconds: number) {
  const whole = Number.isFinite(seconds) ? Math.max(0, Math.floor(seconds)) : 0;
  return `${Math.floor(whole / 60)}:${String(whole % 60).padStart(2, "0")}`;
}
