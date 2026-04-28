import { Router } from "express";
import usersRouter from "./users";
import patchesRouter from "./patches";
import releasesRouter from "./releases";
import tagsRouter from "./tags";

const router = Router();

router.use("/users", usersRouter);
router.use("/patches", patchesRouter);
router.use("/releases", releasesRouter);
router.use("/tags", tagsRouter);

export default router;
