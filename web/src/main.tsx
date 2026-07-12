import React from "react";
import { createRoot } from "react-dom/client";

import { ActivityDashboard } from "./activity/activity-dashboard.component";
import "./index.css";

document.documentElement.classList.add("dark");

const root = document.getElementById("root");
if (!root) throw new Error("root element not found");

createRoot(root).render(
  <React.StrictMode>
  <ActivityDashboard />
  </React.StrictMode>,
);
