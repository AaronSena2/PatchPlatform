import "dotenv/config";
import app from "./app";
import { prisma } from "./db/client";

const PORT = process.env.PORT ? parseInt(process.env.PORT, 10) : 3000;

async function main() {
  await prisma.$connect();
  console.log("Connected to database");

  const server = app.listen(PORT, () => {
    console.log(`PatchPlatform API running on http://localhost:${PORT}`);
  });

  const shutdown = async () => {
    console.log("Shutting down...");
    server.close();
    await prisma.$disconnect();
    process.exit(0);
  };

  process.on("SIGTERM", shutdown);
  process.on("SIGINT", shutdown);
}

main().catch((err) => {
  console.error("Failed to start server:", err);
  process.exit(1);
});
