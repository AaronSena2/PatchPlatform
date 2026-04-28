import { Router, Request, Response, NextFunction } from "express";
import { prisma } from "../db/client";
import { createError } from "../middleware/errorHandler";
import { Role } from "../../generated/prisma";

const router = Router();

// GET /users
router.get("/", async (_req: Request, res: Response, next: NextFunction) => {
  try {
    const users = await prisma.user.findMany({
      select: { id: true, email: true, name: true, role: true, createdAt: true },
      orderBy: { createdAt: "desc" },
    });
    res.json(users);
  } catch (err) {
    next(err);
  }
});

// GET /users/:id
router.get("/:id", async (req: Request, res: Response, next: NextFunction) => {
  try {
    const id = Number(req.params.id);
    if (isNaN(id)) return next(createError("Invalid user ID", 400));

    const user = await prisma.user.findUnique({
      where: { id },
      select: {
        id: true,
        email: true,
        name: true,
        role: true,
        createdAt: true,
        patches: {
          select: { id: true, title: true, version: true, status: true },
        },
      },
    });
    if (!user) return next(createError("User not found", 404));
    res.json(user);
  } catch (err) {
    next(err);
  }
});

// POST /users
router.post("/", async (req: Request, res: Response, next: NextFunction) => {
  try {
    const { email, name, role } = req.body as {
      email?: string;
      name?: string;
      role?: string;
    };
    if (!email || !name) {
      return next(createError("email and name are required", 400));
    }
    if (role && !Object.values(Role).includes(role as Role)) {
      return next(createError(`role must be one of: ${Object.values(Role).join(", ")}`, 400));
    }
    const user = await prisma.user.create({
      data: { email, name, ...(role && { role: role as Role }) },
      select: { id: true, email: true, name: true, role: true, createdAt: true },
    });
    res.status(201).json(user);
  } catch (err: unknown) {
    if (
      typeof err === "object" &&
      err !== null &&
      "code" in err &&
      (err as { code: string }).code === "P2002"
    ) {
      return next(createError("Email already in use", 409));
    }
    next(err);
  }
});

// PATCH /users/:id
router.patch("/:id", async (req: Request, res: Response, next: NextFunction) => {
  try {
    const id = Number(req.params.id);
    if (isNaN(id)) return next(createError("Invalid user ID", 400));

    const { name, role } = req.body as { name?: string; role?: string };
    if (role && !Object.values(Role).includes(role as Role)) {
      return next(createError(`role must be one of: ${Object.values(Role).join(", ")}`, 400));
    }
    const user = await prisma.user.update({
      where: { id },
      data: {
        ...(name && { name }),
        ...(role && { role: role as Role }),
      },
      select: { id: true, email: true, name: true, role: true, updatedAt: true },
    });
    res.json(user);
  } catch (err: unknown) {
    if (
      typeof err === "object" &&
      err !== null &&
      "code" in err &&
      (err as { code: string }).code === "P2025"
    ) {
      return next(createError("User not found", 404));
    }
    next(err);
  }
});

// DELETE /users/:id
router.delete("/:id", async (req: Request, res: Response, next: NextFunction) => {
  try {
    const id = Number(req.params.id);
    if (isNaN(id)) return next(createError("Invalid user ID", 400));

    await prisma.user.delete({ where: { id } });
    res.status(204).send();
  } catch (err: unknown) {
    if (
      typeof err === "object" &&
      err !== null &&
      "code" in err &&
      (err as { code: string }).code === "P2025"
    ) {
      return next(createError("User not found", 404));
    }
    next(err);
  }
});

export default router;
