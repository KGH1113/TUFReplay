const nodes = {
  notice: document.querySelector("#notice"),
  list: document.querySelector("#tester-list"),
  events: document.querySelector("#event-list"),
  activeCount: document.querySelector("#active-count"),
  inactiveCount: document.querySelector("#inactive-count"),
  lastChange: document.querySelector("#last-change"),
  search: document.querySelector("#search"),
  addForm: document.querySelector("#add-form"),
  lookupPlayer: document.querySelector("#lookup-player"),
  playerResult: document.querySelector("#player-result"),
  addTester: document.querySelector("#add-tester"),
  dialog: document.querySelector("#change-dialog"),
  changeForm: document.querySelector("#change-form"),
  dialogTitle: document.querySelector("#dialog-title"),
  dialogDescription: document.querySelector("#dialog-description"),
  confirmChange: document.querySelector("#confirm-change"),
};

let data = { testers: [], events: [] };
let filter = "all";
let pendingChange = null;
let verifiedPlayer = null;
let lookupVersion = 0;
const date = new Intl.DateTimeFormat("ko-KR", { dateStyle: "medium", timeStyle: "short" });

function notice(message, error = false) {
  nodes.notice.textContent = message;
  nodes.notice.classList.toggle("error", error);
}

function element(tag, className, content) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (content !== undefined) node.textContent = content;
  return node;
}

function displayDate(value) {
  return date.format(new Date(value));
}

async function api(path, options = {}) {
  const response = await fetch(path, {
    ...options,
    headers: options.body ? { "Content-Type": "application/json" } : undefined,
    cache: "no-store",
  });
  const body = await response.json();
  if (!response.ok) throw new Error(body.error || "요청을 완료하지 못했습니다.");
  return body;
}

function renderTesters() {
  const query = nodes.search.value.trim().toLowerCase();
  const testers = data.testers.filter((tester) => {
    return (
      (filter === "all" || (filter === "active") === tester.active) &&
      `${tester.label} ${tester.userId}`.toLowerCase().includes(query)
    );
  });
  nodes.list.replaceChildren();
  if (!testers.length) {
    nodes.list.append(element("p", "empty", "조건에 맞는 테스터가 없습니다."));
    return;
  }
  for (const tester of testers) {
    const row = element("article", "tester");
    const main = element("div", "tester-main");
    const heading = element("div", "tester-heading");
    heading.append(element("span", "tester-name", tester.label || "이름 없음"));
    heading.append(element("span", `pill${tester.active ? "" : " inactive"}`, tester.active ? "활성" : "비활성"));
    main.append(heading, element("code", "tester-id", tester.userId));
    main.append(element("p", "tester-meta", `${displayDate(tester.updatedAt)} · ${tester.updatedBy}`));
    const action = element("button", `button ${tester.active ? "button-danger" : "button-secondary"}`, tester.active ? "비활성화" : "재활성화");
    action.type = "button";
    action.addEventListener("click", () => openChange(tester));
    row.append(main, action);
    nodes.list.append(row);
  }
}

function renderEvents() {
  nodes.events.replaceChildren();
  if (!data.events.length) {
    nodes.events.append(element("li", "empty", "변경 이력이 없습니다."));
    return;
  }
  for (const item of data.events) {
    const row = element("li");
    row.append(element("span", `event-dot${item.action === "revoke" ? " revoke" : ""}`));
    const content = element("div");
    content.append(element("div", "event-title", `${item.action === "grant" ? "승인" : "비활성화"} · ${item.userId}`));
    content.append(element("p", "event-detail", `${item.actor} · ${displayDate(item.createdAt)}`));
    content.append(element("p", "event-detail", item.reason));
    row.append(content);
    nodes.events.append(row);
  }
}

function render() {
  const active = data.testers.filter((tester) => tester.active).length;
  nodes.activeCount.textContent = String(active);
  nodes.inactiveCount.textContent = String(data.testers.length - active);
  nodes.lastChange.textContent = data.events.length ? displayDate(data.events[0].createdAt) : "없음";
  renderTesters();
  renderEvents();
}

async function refresh() {
  try {
    data = await api("/api/testers");
    render();
  } catch (error) {
    notice(error.message, true);
  }
}

function openChange(tester) {
  pendingChange = tester;
  const activate = !tester.active;
  nodes.dialogTitle.textContent = activate ? "테스터 재활성화" : "테스터 비활성화";
  nodes.dialogDescription.textContent = activate
    ? `${tester.label || tester.userId} 계정의 제출 권한을 다시 켭니다.`
    : `${tester.label || tester.userId} 계정의 새 실행과 등록을 막습니다. 이미 등록된 패스는 유지됩니다.`;
  nodes.confirmChange.textContent = activate ? "다시 활성화" : "비활성화";
  nodes.confirmChange.className = `button ${activate ? "button-primary" : "button-danger"}`;
  nodes.changeForm.reset();
  nodes.dialog.showModal();
  nodes.changeForm.elements.reason.focus();
}

function clearVerifiedPlayer() {
  lookupVersion += 1;
  verifiedPlayer = null;
  nodes.lookupPlayer.disabled = false;
  nodes.lookupPlayer.textContent = "계정 조회";
  nodes.playerResult.hidden = true;
  nodes.playerResult.replaceChildren();
  nodes.addTester.disabled = true;
}

nodes.addForm.elements.playerId.addEventListener("input", clearVerifiedPlayer);
nodes.lookupPlayer.addEventListener("click", async () => {
  const input = nodes.addForm.elements.playerId;
  if (!input.reportValidity()) return;
  clearVerifiedPlayer();
  const version = lookupVersion;
  const playerId = input.value;
  nodes.lookupPlayer.disabled = true;
  nodes.lookupPlayer.textContent = "조회 중…";
  try {
    const player = await api(`/api/players/${encodeURIComponent(playerId)}`);
    if (version !== lookupVersion) return;
    verifiedPlayer = player;
    nodes.playerResult.replaceChildren(
      element("strong", "", `${player.playerName || "플레이어"} · ${player.username}`),
      element("span", "", `플레이어 #${player.playerId} · TUF 계정 ${player.userId}`),
    );
    nodes.playerResult.hidden = false;
    nodes.addTester.disabled = false;
  } catch (error) {
    if (version === lookupVersion) notice(error.message, true);
  } finally {
    if (version === lookupVersion) {
      nodes.lookupPlayer.disabled = false;
      nodes.lookupPlayer.textContent = "계정 조회";
    }
  }
});

document.querySelector("#refresh").addEventListener("click", () => void refresh());
nodes.search.addEventListener("input", renderTesters);
for (const button of document.querySelectorAll("[data-filter]")) {
  button.addEventListener("click", () => {
    filter = button.dataset.filter;
    for (const item of document.querySelectorAll("[data-filter]")) {
      const selected = item === button;
      item.classList.toggle("selected", selected);
      item.setAttribute("aria-pressed", String(selected));
    }
    renderTesters();
  });
}

nodes.addForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  const player = verifiedPlayer;
  if (!player || String(player.playerId) !== nodes.addForm.elements.playerId.value) {
    notice("플레이어 ID로 계정을 먼저 조회해 주세요.", true);
    return;
  }
  const submit = nodes.addTester;
  submit.disabled = true;
  try {
    const form = new FormData(nodes.addForm);
    await api("/api/testers/by-player", {
      method: "POST",
      body: JSON.stringify({ playerId: player.playerId, expectedUserId: player.userId, label: form.get("label"), reason: form.get("reason") }),
    });
    nodes.addForm.reset();
    clearVerifiedPlayer();
    await refresh();
    notice("테스터 권한을 추가했습니다.");
  } catch (error) {
    clearVerifiedPlayer();
    notice(error.message, true);
  } finally {
    submit.disabled = !verifiedPlayer;
  }
});

document.querySelector("#cancel-change").addEventListener("click", () => nodes.dialog.close());
nodes.changeForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  const tester = pendingChange;
  if (!tester) return;
  nodes.confirmChange.disabled = true;
  try {
    const reason = new FormData(nodes.changeForm).get("reason");
    if (tester.active) {
      await api(`/api/testers/${tester.userId}/revoke`, { method: "POST", body: JSON.stringify({ reason }) });
    } else {
      await api("/api/testers", {
        method: "POST",
        body: JSON.stringify({ userId: tester.userId, label: tester.label, reason }),
      });
    }
    nodes.dialog.close();
    await refresh();
    notice(tester.active ? "테스터 권한을 비활성화했습니다." : "테스터 권한을 다시 활성화했습니다.");
  } catch (error) {
    notice(error.message, true);
  } finally {
    nodes.confirmChange.disabled = false;
  }
});

void refresh();
