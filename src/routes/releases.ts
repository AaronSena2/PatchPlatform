import { Router, Request, Response, NextFunction } from "express";
import { prisma } from "../db/client";
import { createError } from "../middleware/errorHandler";

const router = Router();

// GET /releases
router.get("/", async (_req: Request, res: Response, next: NextFunction) => {
  try {
    const releases = await prisma.release.findMany({
      include: {
        patches: {
          select: { id: true, title: true, version: true, status: true },
        },
      },
      orderBy: { createdAt: "desc" },
    });
    res.json(releases);
  } catch (err) {
    next(err);
  }
});

// GET /releases/:id
router.get("/:id", async (req: Request, res: Response, next: NextFunction) => {
  try {
    const id = Number(req.params.id);
    if (isNaN(id)) return next(createError("Invalid release ID", 400));

    const release = await prisma.release.findUnique({
      where: { id },
      include: {
        patches: {
          include: {
            author: { select: { id: true, name: true } },
            tags: { include: { tag: { select: { id: true, name: true } } } },
          },
        },
      },
    });
    if (!release) return next(createError("Release not found", 404));
    res.json(release);
  } catch (err) {
    next(err);
  }
});

// POST /releases
router.post("/", async (req: Request, res: Response, next: NextFunction) => {
  try {
    const { version, name, description, releasedAt } = req.body as {
      version?: string;
      name?: string;
      description?: string;
      releasedAt?: string;
    };

    if (!version || !name) {
      return next(createError("version and name are required", 400));
    }

    const release = await prisma.release.create({
      data: {
        version,
        name,
        ...(description !== undefined && { description }),
        ...(releasedAt && { releasedAt: new Date(releasedAt) }),
      },
    });
    res.status(201).json(release);
  } catch (err: unknown) {
    if (
      typeof err === "object" &&
      err !== null &&
      "code" in err &&
      (err as { code: string }).code === "P2002"
    ) {
      return next(createError("Release version already exists", 409));
    }
    next(err);
  }
});

// PATCH /releases/:id
router.patch("/:id", async (req: Request, res: Response, next: NextFunction) => {
  try {
    const id = Number(req.params.id);
    if (isNaN(id)) return next(createError("Invalid release ID", 400));

    const { name, description, releasedAt } = req.body as {
      name?: string;
      description?: string;
      releasedAt?: string | null;
    };

    const release = await prisma.release.update({
      where: { id },
      data: {
        ...(name && { name }),
        ...(description !== undefined && { description }),
        ...(releasedAt !== undefined && {
          releasedAt: releasedAt ? new Date(releasedAt) : null,
        }),
      },
    });
    res.json(release);
  } catch (err: unknown) {
    if (
      typeof err === "object" &&
      err !== null &&
      "code" in err &&
      (err as { code: string }).code === "P2025"
    ) {
      return next(createError("Release not found", 404));
    }
    next(err);
  }
});

// DELETE /releases/:id
router.delete("/:id", async (req: Request, res: Response, next: NextFunction) => {
  try {
    const id = Number(req.params.id);
    if (isNaN(id)) return next(createError("Invalid release ID", 400));

    await prisma.release.delete({ where: { id } });
    res.status(204).send();
  } catch (err: unknown) {
    if (
      typeof err === "object" &&
      err !== null &&
      "code" in err &&
      (err as { code: string }).code === "P2025"
    ) {
      return next(createError("Release not found", 404));
    }
    next(err);
  }
});

export default router;
