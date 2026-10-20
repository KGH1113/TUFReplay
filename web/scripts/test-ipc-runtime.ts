import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { once } from "node:events";
import {
  type Health,
  type ReplayApiAppHandlers,
  ReplayApiContract,
  type ReplayState,
} from "@tuf-replay/ipc";
import { type AppRegistration, createIpc, ReceivedFile } from "adofai-ipc";

const fixture = process.env.TUFREPLAY_IPC_FIXTURE;
assert.ok(fixture, "Run ./scripts/run.sh ipc-test to build the standalone fixture.");
const dotnet = process.env.DOTNET_EXE ?? "dotnet";
const apps: AppRegistration[] = [];
const children: ReturnType<typeof spawn>[] = [];
const pause = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));
async function until(check: () => boolean | Promise<boolean>, message: string) {
  const deadline = Date.now() + 12_000;
  while (!(await check())) {
    assert.ok(Date.now() < deadline, message);
    await pause(25);
  }
}
async function launch() {
  const child = spawn(dotnet, [fixture as string], { stdio: ["pipe", "pipe", "pipe"] });
  children.push(child);
  let stdout = "";
  let stderr = "";
  let launchError: Error | undefined;
  child.once("error", (error) => {
    launchError = error;
  });
  child.stdout.on("data", (data) => {
    stdout += data;
  });
  child.stderr.on("data", (data) => {
    stderr += data;
  });
  await until(
    () => /IPC_TEST_PORT=(\d+)/.test(stdout) || child.exitCode !== null || !!launchError,
    "The C# fixture did not start.",
  );
  assert.equal(launchError, undefined);
  assert.equal(child.exitCode, null, stderr);
  const port = Number(stdout.match(/IPC_TEST_PORT=(\d+)/)?.[1]);
  return { child, port, output: () => stdout };
}
const noOp = () => {};
const handlers: ReplayApiAppHandlers = {
  activityChanged: noOp,
  calibrationStateChanged: noOp,
  downloadsStateChanged: noOp,
  ffmpegStateChanged: noOp,
  healthSnapshot: noOp,
  microphoneDevicesChanged: noOp,
  microphoneTimingChanged: noOp,
  renderBundleStateChanged: noOp,
  replayStateChanged: noOp,
  webcamStateChanged: noOp,
};
function code(error: unknown) {
  return typeof error === "object" && error !== null && "code" in error ? error.code : undefined;
}

try {
  let server = await launch();
  const initialPort = server.port;
  function register(name: string, requestTimeoutMs = 5_000) {
    const app = createIpc({
      startPort: initialPort,
      endPort: initialPort,
      connectTimeoutMs: 1_000,
      heartbeatIntervalMs: 100,
      heartbeatTimeoutMs: 500,
      requestTimeoutMs,
    }).registerApp({ displayName: name });
    apps.push(app);
    const snapshots: Health[] = [];
    const states: ReplayState[] = [];
    const channel = app.use(ReplayApiContract, {
      ...handlers,
      healthSnapshot: ({ value }) => {
        snapshots.push(value);
      },
      replayStateChanged: ({ value }) => {
        states.push(value);
      },
    });
    return { app, channel, snapshots, states };
  }
  const first = register("TUFReplay integration A");
  const second = register("TUFReplay integration B");
  await Promise.all([first.channel.ready(), second.channel.ready()]);
  await until(
    () => first.states.length === 1 && second.states.length === 1,
    "Initial snapshots missing.",
  );
  assert.equal(first.snapshots[0]?.modVersion, "fixture");
  assert.equal((await first.channel.readHealth()).protocolVersion, 11);
  const firstSession = first.channel.sessionId;
  await first.channel.startReplay({ runId: second.app.appId });
  assert.equal(first.states.length, 1, "A selected-app event leaked to its requester.");
  assert.equal(second.states.at(-1)?.runId, second.app.appId);
  assert.equal(second.states.at(-1)?.state, "playing");

  const firstFile = await first.channel.exportMicrophoneRecording({ runId: "private A 한글" });
  const secondFile = await second.channel.exportMicrophoneRecording({ runId: "private B" });
  assert.ok(firstFile instanceof ReceivedFile);
  assert.equal(await firstFile.text(), "private A 한글");
  assert.equal(await secondFile.text(), "private B");
  await assert.rejects(async () => firstFile.text(), TypeError);
  const chart = await first.channel.readLogicalLevelChart({ id: "chart-session" });
  assert.equal(chart.levelSessionId, "chart-session");
  assert.equal(chart.floorCount, 4896);
  assert.equal(await chart.file.text(), `{"comment":"${"한".repeat(1_500_000)}🙂"}`);
  const video = await first.channel.exportCameraRecording({ runId: "video" });
  const reader = video.stream().getReader();
  let bytes = 0;
  for (;;) {
    const { done, value } = await reader.read();
    if (done) break;
    assert.ok(value.every((byte) => byte === 0x5a));
    bytes += value.length;
  }
  assert.equal(bytes, 8 * 1024 * 1024);
  const abort = new AbortController();
  const cancelledFile = await first.channel.exportCameraRecording({ runId: "cancel" });
  const cancelledReader = cancelledFile.stream({ signal: abort.signal }).getReader();
  await cancelledReader.read();
  abort.abort();
  await assert.rejects(cancelledReader.read());

  const timed = register("TUFReplay timeout", 300);
  await timed.channel.ready();
  await assert.rejects(
    timed.channel.chooseLevelFile({ runId: "wait" }),
    (error) => code(error) === "timeout",
  );
  // A call timeout stops waiting; it does not undo a mod operation. Disposing the
  // originating app is what cancels the session-owned native picker here.
  timed.app.dispose();
  await until(
    async () => (await first.channel.readHealth()).serverVersion >= 1,
    "Disconnect after timeout did not cancel the mod handler.",
  );
  const disposing = register("TUFReplay disposed request");
  await disposing.channel.ready();
  const disposedRequest = disposing.channel.chooseLevelFile({ runId: "wait" });
  const disposedResult = assert.rejects(disposedRequest);
  await until(
    async () => (await first.channel.readLegacyReplayStatus()).hasLegacyReplays,
    "Pending request not started.",
  );
  disposing.app.dispose();
  await disposedResult;
  await until(
    async () => (await first.channel.readHealth()).serverVersion >= 2,
    "Disconnect did not cancel the mod handler.",
  );

  const stale = await first.channel.exportMicrophoneRecording({ runId: "stale" });
  const pending = assert.rejects(first.channel.chooseLevelFile({ runId: "wait" }));
  await until(
    async () => (await second.channel.readLegacyReplayStatus()).hasLegacyReplays,
    "Restart request not started.",
  );
  const exited = once(server.child, "exit");
  server.child.stdin.write("stop\n");
  await exited;
  await pending;
  await until(() => first.channel.status !== "ready", "Session remained ready after disconnect.");
  await assert.rejects(async () => stale.text());
  server = await launch();
  assert.equal(server.port, initialPort, "Fixture restart did not recover its port.");
  await until(
    () => first.snapshots.length === 2 && second.snapshots.length === 2,
    "Reconnect snapshots missing.",
  );
  assert.notEqual(first.channel.sessionId, firstSession);
  assert.equal(first.states.at(-1)?.state, "idle");
  assert.equal(
    await (await first.channel.exportMicrophoneRecording({ runId: "fresh" })).text(),
    "fresh",
  );

  const disabledFile = await first.channel.exportMicrophoneRecording({ runId: "disable" });
  const disableRequest = assert.rejects(first.channel.chooseLevelFile({ runId: "wait" }));
  await until(
    async () => (await second.channel.readLegacyReplayStatus()).hasLegacyReplays,
    "Disable request not started.",
  );
  server.child.stdin.write("disable\n");
  await until(() => server.output().includes("IPC_TEST_DISABLED"), "Mod disable not acknowledged.");
  await disableRequest;
  await until(
    () => first.channel.status === "unavailable",
    "Mod disable did not invalidate the channel.",
  );
  await assert.rejects(async () => disabledFile.text());
  console.log(
    "Passed real IPC integration: typed requests/callbacks, selected app, single-use and large files, file/request cancellation, disconnect, reconnect snapshots, and mod disable.",
  );
} finally {
  for (const app of apps) app.dispose();
  for (const child of children) {
    if (child.exitCode === null && child.signalCode === null) child.kill("SIGTERM");
  }
}
