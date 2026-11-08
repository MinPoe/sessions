// Sessions browser relay, service worker
//
// Holds a long-lived native messaging port to the Sessions host, an open port
// keeps this service worker alive, on {cmd:"getWindows"} it replies with the open
// http/https tab urls grouped by Chrome window, see docs/spikes/browser.md

const HOST = "com.sessions.browser";
let port = null;

function connect() {
  try {
    port = chrome.runtime.connectNative(HOST);
    port.onMessage.addListener(onMessage);
    port.onDisconnect.addListener(onDisconnect);
  } catch (e) {
    port = null;
    // Host not registered yet or launch refused, retry later
    setTimeout(connect, 5000);
  }
}

function onDisconnect() {
  port = null;
  // The host exits when Chrome closes the port, reconnect so we are ready again
  setTimeout(connect, 1000);
}

async function onMessage(message) {
  if (!message || message.cmd !== "getWindows") {
    return;
  }

  const all = await chrome.windows.getAll({ populate: true });
  const windows = all
    .filter((w) => w.type === "normal")
    .map((w) => {
      const tabs = w.tabs || [];
      const active = tabs.find((t) => t.active);
      return {
        id: w.id,
        focused: w.focused === true,
        // active tab title, lets the app match this to the OS window title
        title: active && typeof active.title === "string" ? active.title : "",
        urls: tabs
          .map((t) => t.url)
          .filter((u) => typeof u === "string" && /^https?:/i.test(u)),
      };
    })
    .filter((w) => w.urls.length > 0);

  if (port) {
    port.postMessage({ windows });
  }
}

// Connect on worker wake, on browser start, and on install, whichever comes first
chrome.runtime.onStartup.addListener(connect);
chrome.runtime.onInstalled.addListener(connect);
connect();
