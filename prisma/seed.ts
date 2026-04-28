import "dotenv/config";
import { PrismaClient, Role, PatchStatus } from "../generated/prisma";

const prisma = new PrismaClient();

async function main() {
  console.log("Seeding database...");

  // Create users
  const admin = await prisma.user.upsert({
    where: { email: "admin@patchplatform.dev" },
    update: {},
    create: {
      email: "admin@patchplatform.dev",
      name: "Admin User",
      role: Role.ADMIN,
    },
  });

  const developer = await prisma.user.upsert({
    where: { email: "dev@patchplatform.dev" },
    update: {},
    create: {
      email: "dev@patchplatform.dev",
      name: "Jane Developer",
      role: Role.DEVELOPER,
    },
  });

  console.log(`Created users: ${admin.name}, ${developer.name}`);

  // Create tags
  const [security, bugfix, performance] = await Promise.all([
    prisma.tag.upsert({
      where: { name: "security" },
      update: {},
      create: { name: "security" },
    }),
    prisma.tag.upsert({
      where: { name: "bugfix" },
      update: {},
      create: { name: "bugfix" },
    }),
    prisma.tag.upsert({
      where: { name: "performance" },
      update: {},
      create: { name: "performance" },
    }),
  ]);

  console.log("Created tags: security, bugfix, performance");

  // Create a release
  const release = await prisma.release.upsert({
    where: { version: "1.0.0" },
    update: {},
    create: {
      version: "1.0.0",
      name: "Initial Release",
      description: "First stable release of PatchPlatform",
      releasedAt: new Date("2024-01-01"),
    },
  });

  console.log(`Created release: ${release.name}`);

  // Create patches
  const patch1 = await prisma.patch.create({
    data: {
      title: "Fix authentication bypass vulnerability",
      description: "Resolves a critical security issue allowing unauthenticated access.",
      version: "1.0.1",
      status: PatchStatus.DEPLOYED,
      authorId: developer.id,
      releaseId: release.id,
      tags: {
        create: [{ tagId: security.id }, { tagId: bugfix.id }],
      },
    },
  });

  const patch2 = await prisma.patch.create({
    data: {
      title: "Improve query performance on large datasets",
      description: "Added indexes and optimized queries for better throughput.",
      version: "1.0.2",
      status: PatchStatus.APPROVED,
      authorId: developer.id,
      tags: {
        create: [{ tagId: performance.id }],
      },
    },
  });

  const patch3 = await prisma.patch.create({
    data: {
      title: "Update dependency vulnerabilities",
      description: "Bumps vulnerable dependencies to their latest safe versions.",
      version: "1.0.3",
      status: PatchStatus.PENDING,
      authorId: admin.id,
      tags: {
        create: [{ tagId: security.id }],
      },
    },
  });

  console.log(
    `Created patches: "${patch1.title}", "${patch2.title}", "${patch3.title}"`
  );
  console.log("Seeding complete.");
}

main()
  .catch((err) => {
    console.error("Seed failed:", err);
    process.exit(1);
  })
  .finally(() => prisma.$disconnect());
