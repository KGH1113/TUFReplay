import React from "react";
import { createRoot } from "react-dom/client";

import { AppRoot } from "./app/app-root";
import { initializeI18n } from "./i18n/i18n";
import "./index.css";

document.documentElement.classList.add("dark");
await initializeI18n();

const root = document.getElementById("root");
if (!root) throw new Error("root element not found");

createRoot(root).render(
  <React.StrictMode>
    <AppRoot />
  </React.StrictMode>,
);
