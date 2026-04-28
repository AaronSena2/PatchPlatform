import "dotenv/config";
import { defineConfig } from "prisma/config";

function buildDatabaseUrl(): string {
  return (
    process.env.DATABASE_URL ??
    `mysql://${process.env.MYSQL_USER ?? "patch_user"}:${process.env.MYSQL_PASSWORD ?? "patch_password"}@${process.env.MYSQL_HOST ?? "localhost"}:${process.env.MYSQL_PORT ?? "3306"}/${process.env.MYSQL_DATABASE ?? "patchplatform"}`
  );
}

export default defineConfig({
  schema: "prisma/schema.prisma",
  migrations: {
    path: "prisma/migrations",
  },
  datasource: {
    url: buildDatabaseUrl(),
  },
});

