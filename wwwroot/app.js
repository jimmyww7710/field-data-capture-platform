const state = { events: [], page: 1, pageSize: 25 };

const elements = {
  healthStatus: document.querySelector("#healthStatus"),
  totalEvents: document.querySelector("#totalEvents"),
  deviceCount: document.querySelector("#deviceCount"),
  pendingCount: document.querySelector("#pendingCount"),
  approvedCount: document.querySelector("#approvedCount"),
  metricChart: document.querySelector("#metricChart"),
  eventsBody: document.querySelector("#eventsBody"),
  emptyState: document.querySelector("#emptyState"),
  backupsList: document.querySelector("#backupsList"),
  searchInput: document.querySelector("#searchInput"),
  statusFilter: document.querySelector("#statusFilter"),
  toast: document.querySelector("#toast"),
  guide: document.querySelector(".workflow-guide"),
  chartUpdate: document.querySelector("#chartUpdate")
};

async function api(path, options = {}) {
  const response = await fetch(path, {
    headers: { "Content-Type": "application/json", ...(options.headers || {}) },
    ...options
  });

  if (!response.ok) {
    const body = await response.json().catch(() => ({ error: "Request failed." }));
    throw new Error(body.error || "Request failed.");
  }

  return response.status === 204 ? null : response.json();
}

function showToast(message) {
  elements.toast.textContent = message;
  elements.toast.classList.add("visible");
  window.clearTimeout(showToast.timer);
  showToast.timer = window.setTimeout(() => elements.toast.classList.remove("visible"), 2600);
}

function formatDate(value) {
  return new Date(value).toLocaleString();
}

function badge(status) {
  return `<span class="badge ${status.toLowerCase()}">${status}</span>`;
}

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

async function loadHealth() {
  try {
    await api("/api/health");
    elements.healthStatus.textContent = "Service healthy";
    elements.healthStatus.className = "status-badge healthy";
  } catch {
    elements.healthStatus.textContent = "Service unavailable";
  }
}

async function loadEvents() {
  const params = new URLSearchParams({
    page: String(state.page),
    pageSize: String(state.pageSize)
  });
  const search = elements.searchInput.value.trim();
  const status = elements.statusFilter.value;
  if (search) params.set("search", search);
  if (status) params.set("status", status);

  const result = await api(`/api/events?${params}`);
  state.events = result.items;

  elements.eventsBody.innerHTML = state.events.map(event => `
    <tr>
      <td>${formatDate(event.capturedAt)}</td>
      <td>${escapeHtml(event.deviceId)}</td>
      <td>${escapeHtml(event.eventType)}</td>
      <td>${escapeHtml(event.metric)}</td>
      <td>${event.value} ${escapeHtml(event.unit)}</td>
      <td>${badge(event.status)}</td>
      <td>
        <button class="button approve" data-review="Approved" data-id="${event.id}">Approve</button>
        <button class="button reject" data-review="Rejected" data-id="${event.id}">Reject</button>
      </td>
    </tr>
  `).join("");

  elements.emptyState.style.display = state.events.length ? "none" : "block";

  const allStatusResult = await api("/api/events?page=1&pageSize=200");
  const allEvents = allStatusResult.items;
  elements.totalEvents.textContent = allStatusResult.totalCount;
  elements.deviceCount.textContent = new Set(allEvents.map(event => event.deviceId)).size;
  elements.pendingCount.textContent = allEvents.filter(event => event.status === "Pending").length;
  elements.approvedCount.textContent = allEvents.filter(event => event.status === "Approved").length;
  renderChart(allEvents.slice(0, 20));
  updateWorkflowProgress(allEvents.length);
  if (allEvents.length)
    elements.chartUpdate.textContent = `Updated ${new Date().toLocaleTimeString()}`;
}

function renderChart(events) {
  if (!events.length) {
    elements.metricChart.innerHTML = `<div class="empty-state"><strong>No readings yet</strong><span>Generate test records to see live measurements here.</span></div>`;
    elements.chartUpdate.textContent = "Waiting for data";
    return;
  }

  const max = Math.max(...events.map(event => event.value), 1);
  elements.metricChart.innerHTML = events.slice().reverse().map(event =>
    `<div class="bar" style="height:${Math.max(8, event.value / max * 155)}px" title="${escapeHtml(event.metric)}: ${event.value} ${escapeHtml(event.unit)}"></div>`
  ).join("");
}

async function loadBackups() {
  const backups = await api("/api/backups");
  elements.backupsList.innerHTML = backups.length
    ? backups.map(backup => `
      <div class="backup-row">
        <div>
          <strong>${formatDate(backup.createdAt)}</strong>
          <div>${backup.eventCount} events · ${backup.sizeBytes} bytes</div>
        </div>
        <span>${escapeHtml(backup.id)}</span>
        <button class="button secondary" data-restore="${backup.id}">Restore Data</button>
      </div>
    `).join("")
    : `<div class="empty-state"><strong>No backups yet</strong><span>After reviewing records, create a backup to preserve this state.</span></div>`;
}

function updateWorkflowProgress(eventCount) {
  const firstStep = document.querySelector('[data-step="1"]');
  const secondStep = document.querySelector('[data-step="2"]');
  firstStep?.classList.toggle("completed", eventCount > 0);
  secondStep?.classList.toggle("completed", eventCount > 0 && (
    document.querySelector("#pendingCount").textContent !== String(eventCount)
  ));
}

function markStepCompleted(stepNumber) {
  document.querySelector(`[data-step="${stepNumber}"]`)?.classList.add("completed");
}

document.querySelector("#dismissGuide").addEventListener("click", () => {
  elements.guide.classList.add("hidden");
  window.localStorage.setItem("hideFieldDataGuide", "true");
});

if (window.localStorage.getItem("hideFieldDataGuide") === "true")
  elements.guide.classList.add("hidden");

document.querySelector("#simulateButton").addEventListener("click", async event => {
  event.target.disabled = true;
  try {
    const count = Number(document.querySelector("#eventCount").value);
    const deviceId = document.querySelector("#deviceId").value;
    await api("/api/simulator", {
      method: "POST",
      body: JSON.stringify({ deviceId, count })
    });
    showToast(`${count} test records were generated.`);
    markStepCompleted("1");
    await loadEvents();
  } catch (error) {
    showToast(error.message);
  } finally {
    event.target.disabled = false;
  }
});

document.querySelector("#filterButton").addEventListener("click", loadEvents);

document.querySelector("#backupButton").addEventListener("click", async event => {
  if (elements.totalEvents.textContent === "0") {
    showToast("Generate test records before creating a backup.");
    return;
  }

  event.target.disabled = true;
  try {
    await api("/api/backups", { method: "POST" });
    showToast("Backup created successfully.");
    await loadBackups();
    markStepCompleted("3");
  } catch (error) {
    showToast(error.message);
  } finally {
    event.target.disabled = false;
  }
});

document.querySelector("#resetButton").addEventListener("click", async () => {
  if (!window.confirm("Clear all demo records? Existing backups will be kept."))
    return;

  await api("/api/demo/reset", { method: "POST" });
  showToast("Demo records were cleared.");
  await Promise.all([loadEvents(), loadBackups()]);
});

elements.eventsBody.addEventListener("click", async event => {
  const button = event.target.closest("[data-review]");
  if (!button) return;

  try {
    await api(`/api/events/${button.dataset.id}/review`, {
      method: "POST",
      body: JSON.stringify({
        status: button.dataset.review,
        reviewer: "Operations User",
        note: "Reviewed in the demo console."
      })
    });
    showToast(`Event marked ${button.dataset.review.toLowerCase()}`);
    markStepCompleted("2");
    await loadEvents();
  } catch (error) {
    showToast(error.message);
  }
});

elements.backupsList.addEventListener("click", async event => {
  const button = event.target.closest("[data-restore]");
  if (!button) return;
  if (!window.confirm("Restore this backup and replace the current records?"))
    return;

  await api(`/api/backups/${button.dataset.restore}/restore`, { method: "POST" });
  showToast("Backup restored successfully.");
  await loadEvents();
});

function initializeSignalR() {
  if (!window.signalR) return;

  const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/events")
    .withAutomaticReconnect()
    .build();

  connection.on("eventCaptured", loadEvents);
  connection.on("dataChanged", loadEvents);
  connection.start().catch(error => console.error("SignalR connection failed:", error));
}

loadHealth();
loadEvents();
loadBackups();
initializeSignalR();
