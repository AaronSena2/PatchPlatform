import { PrismaClient } from "../../generated/prisma";
import { PrismaMariaDb } from "@prisma/adapter-mariadb";

function buildDatabaseUrl(): string {
  return (
    process.env.DATABASE_URL ??
    `mysql://${process.env.MYSQL_USER ?? "patch_user"}:${process.env.MYSQL_PASSWORD ?? "patch_password"}@${process.env.MYSQL_HOST ?? "localhost"}:${process.env.MYSQL_PORT ?? "3306"}/${process.env.MYSQL_DATABASE ?? "patchplatform"}`
  );
}

function createPrismaClient(): PrismaClient {
  const adapter = new PrismaMariaDb(buildDatabaseUrl());
  return new PrismaClient({
    adapter,
    log:
      process.env.NODE_ENV === "development"
        ? ["query", "error", "warn"]
        : ["error"],
  });
}

const globalForPrisma = globalThis as unknown as {
  prisma: PrismaClient | undefined;
};

export const prisma = globalForPrisma.prisma ?? createPrismaClient();

if (process.env.NODE_ENV !== "production") {
  globalForPrisma.prisma = prisma;
}


