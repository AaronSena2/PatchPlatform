import { Router, Request, Response, NextFunction } from "express";
import { prisma } from "../db/client";
import { createError } from "../middleware/errorHandler";

const router = Router();

// GET /tags
router.get("/", async (_req: Request, res: Response, next: NextFunction) => {
  try {
    const tags = await prisma.tag.findMany({
      orderBy: { name: "asc" },
    });
    res.json(tags);
  } catch (err) {
    next(err);
  }
});

// GET /tags/:id
router.get("/:id", async (req: Request, res: Response, next: NextFunction) => {
  try {
    const id = Number(req.params.id);
    if (isNaN(id)) return next(createError("Invalid tag ID", 400));

    const tag = await prisma.tag.findUnique({
      where: { id },
      include: {
        patches: {
          include: {
            patch: { select: { id: true, title: true, version: true, status: true } },
          },
        },
      },
    });
    if (!tag) return next(createError("Tag not found", 404));
    res.json(tag);
  } catch (err) {
    next(err);
  }
});

// POST /tags
router.post("/", async (req: Request, res: Response, next: NextFunction) => {
  try {
    const { name } = req.body as { name?: string };
    if (!name) return next(createError("name is required", 400));

    const tag = await prisma.tag.create({ data: { name } });
    res.status(201).json(tag);
  } catch (err: unknown) {
    if (
      typeof err === "object" &&
      err !== null &&
      "code" in err &&
      (err as { code: string }).code === "P2002"
    ) {
      return next(createError("Tag name already exists", 409));
    }
    next(err);
  }
});

// DELETE /tags/:id
router.delete("/:id", async (req: Request, res: Response, next: NextFunction) => {
  try {
    const id = Number(req.params.id);
    if (isNaN(id)) return next(createError("Invalid tag ID", 400));

    await prisma.tag.delete({ where: { id } });
    res.status(204).send();
  } catch (err: unknown) {
    if (
      typeof err === "object" &&
      err !== null &&
      "code" in err &&
      (err as { code: string }).code === "P2025"
    ) {
      return next(createError("Tag not found", 404));
    }
    next(err);
  }
});

export default router;
