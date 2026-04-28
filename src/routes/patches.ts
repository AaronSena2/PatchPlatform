import { Router, Request, Response, NextFunction } from "express";
import { prisma } from "../db/client";
import { createError } from "../middleware/errorHandler";
import { PatchStatus } from "../../generated/prisma";

const router = Router();

// GET /patches
router.get("/", async (req: Request, res: Response, next: NextFunction) => {
  try {
    const { status, authorId, releaseId } = req.query as {
      status?: string;
      authorId?: string;
      releaseId?: string;
    };

    const patches = await prisma.patch.findMany({
      where: {
        ...(status && { status: status as PatchStatus }),
        ...(authorId && { authorId: Number(authorId) }),
        ...(releaseId && { releaseId: Number(releaseId) }),
      },
      include: {
        author: { select: { id: true, name: true, email: true } },
        release: { select: { id: true, version: true, name: true } },
        tags: { include: { tag: { select: { id: true, name: true } } } },
      },
      orderBy: { createdAt: "desc" },
    });
    res.json(patches);
  } catch (err) {
    next(err);
  }
});

// GET /patches/:id
router.get("/:id", async (req: Request, res: Response, next: NextFunction) => {
  try {
    const id = Number(req.params.id);
    if (isNaN(id)) return next(createError("Invalid patch ID", 400));

    const patch = await prisma.patch.findUnique({
      where: { id },
      include: {
        author: { select: { id: true, name: true, email: true } },
        release: { select: { id: true, version: true, name: true } },
        tags: { include: { tag: { select: { id: true, name: true } } } },
      },
    });
    if (!patch) return next(createError("Patch not found", 404));
    res.json(patch);
  } catch (err) {
    next(err);
  }
});

// POST /patches
router.post("/", async (req: Request, res: Response, next: NextFunction) => {
  try {
    const { title, description, version, authorId, releaseId, tagIds } =
      req.body as {
        title?: string;
        description?: string;
        version?: string;
        authorId?: number;
        releaseId?: number;
        tagIds?: number[];
      };

    if (!title || !version || !authorId) {
      return next(createError("title, version and authorId are required", 400));
    }

    const patch = await prisma.patch.create({
      data: {
        title,
        version,
        authorId,
        ...(description !== undefined && { description }),
        ...(releaseId !== undefined && { releaseId }),
        ...(tagIds?.length && {
          tags: {
            create: tagIds.map((tagId) => ({ tagId })),
          },
        }),
      },
      include: {
        author: { select: { id: true, name: true, email: true } },
        release: { select: { id: true, version: true, name: true } },
        tags: { include: { tag: { select: { id: true, name: true } } } },
      },
    });
    res.status(201).json(patch);
  } catch (err: unknown) {
    if (
      typeof err === "object" &&
      err !== null &&
      "code" in err &&
      (err as { code: string }).code === "P2003"
    ) {
      return next(createError("Author, release, or tag not found", 400));
    }
    next(err);
  }
});

// PATCH /patches/:id
router.patch("/:id", async (req: Request, res: Response, next: NextFunction) => {
  try {
    const id = Number(req.params.id);
    if (isNaN(id)) return next(createError("Invalid patch ID", 400));

    const { title, description, version, status, releaseId } = req.body as {
      title?: string;
      description?: string;
      version?: string;
      status?: string;
      releaseId?: number | null;
    };

    if (status && !Object.values(PatchStatus).includes(status as PatchStatus)) {
      return next(
        createError(
          `status must be one of: ${Object.values(PatchStatus).join(", ")}`,
          400
        )
      );
    }

    const patch = await prisma.patch.update({
      where: { id },
      data: {
        ...(title && { title }),
        ...(description !== undefined && { description }),
        ...(version && { version }),
        ...(status && { status: status as PatchStatus }),
        ...(releaseId !== undefined && { releaseId }),
      },
      include: {
        author: { select: { id: true, name: true, email: true } },
        release: { select: { id: true, version: true, name: true } },
        tags: { include: { tag: { select: { id: true, name: true } } } },
      },
    });
    res.json(patch);
  } catch (err: unknown) {
    if (
      typeof err === "object" &&
      err !== null &&
      "code" in err &&
      (err as { code: string }).code === "P2025"
    ) {
      return next(createError("Patch not found", 404));
    }
    next(err);
  }
});

// DELETE /patches/:id
router.delete("/:id", async (req: Request, res: Response, next: NextFunction) => {
  try {
    const id = Number(req.params.id);
    if (isNaN(id)) return next(createError("Invalid patch ID", 400));

    await prisma.patch.delete({ where: { id } });
    res.status(204).send();
  } catch (err: unknown) {
    if (
      typeof err === "object" &&
      err !== null &&
      "code" in err &&
      (err as { code: string }).code === "P2025"
    ) {
      return next(createError("Patch not found", 404));
    }
    next(err);
  }
});

export default router;
