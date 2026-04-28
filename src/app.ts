import "dotenv/config";
import express from "express";
import cors from "cors";
import helmet from "helmet";
import router from "./routes";
import { errorHandler, notFound } from "./middleware/errorHandler";

const app = express();

app.use(helmet());
app.use(cors());
app.use(express.json());

// Health check
app.get("/health", (_req, res) => {
  res.json({ status: "ok", timestamp: new Date().toISOString() });
});

// API routes
app.use("/api/v1", router);

// 404 handler
app.use(notFound);

// Error handler
app.use(errorHandler);

export default app;
