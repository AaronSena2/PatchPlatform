-- CreateTable: users
CREATE TABLE `users` (
    `id` INTEGER NOT NULL AUTO_INCREMENT,
    `email` VARCHAR(255) NOT NULL,
    `name` VARCHAR(255) NOT NULL,
    `role` ENUM('ADMIN', 'DEVELOPER', 'VIEWER') NOT NULL DEFAULT 'DEVELOPER',
    `createdAt` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    `updatedAt` DATETIME(3) NOT NULL,

    UNIQUE INDEX `users_email_key`(`email`),
    PRIMARY KEY (`id`)
) DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- CreateTable: releases
CREATE TABLE `releases` (
    `id` INTEGER NOT NULL AUTO_INCREMENT,
    `version` VARCHAR(50) NOT NULL,
    `name` VARCHAR(255) NOT NULL,
    `description` TEXT NULL,
    `releasedAt` DATETIME(3) NULL,
    `createdAt` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    `updatedAt` DATETIME(3) NOT NULL,

    UNIQUE INDEX `releases_version_key`(`version`),
    PRIMARY KEY (`id`)
) DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- CreateTable: patches
CREATE TABLE `patches` (
    `id` INTEGER NOT NULL AUTO_INCREMENT,
    `title` VARCHAR(255) NOT NULL,
    `description` TEXT NULL,
    `version` VARCHAR(50) NOT NULL,
    `status` ENUM('PENDING', 'IN_REVIEW', 'APPROVED', 'REJECTED', 'DEPLOYED') NOT NULL DEFAULT 'PENDING',
    `authorId` INTEGER NOT NULL,
    `releaseId` INTEGER NULL,
    `createdAt` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    `updatedAt` DATETIME(3) NOT NULL,

    INDEX `patches_authorId_fkey`(`authorId`),
    INDEX `patches_releaseId_fkey`(`releaseId`),
    PRIMARY KEY (`id`)
) DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- CreateTable: tags
CREATE TABLE `tags` (
    `id` INTEGER NOT NULL AUTO_INCREMENT,
    `name` VARCHAR(100) NOT NULL,
    `createdAt` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),

    UNIQUE INDEX `tags_name_key`(`name`),
    PRIMARY KEY (`id`)
) DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- CreateTable: patch_tags
CREATE TABLE `patch_tags` (
    `patchId` INTEGER NOT NULL,
    `tagId` INTEGER NOT NULL,

    PRIMARY KEY (`patchId`, `tagId`)
) DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- AddForeignKey: patches.authorId -> users.id
ALTER TABLE `patches` ADD CONSTRAINT `patches_authorId_fkey` FOREIGN KEY (`authorId`) REFERENCES `users`(`id`) ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey: patches.releaseId -> releases.id
ALTER TABLE `patches` ADD CONSTRAINT `patches_releaseId_fkey` FOREIGN KEY (`releaseId`) REFERENCES `releases`(`id`) ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey: patch_tags.patchId -> patches.id
ALTER TABLE `patch_tags` ADD CONSTRAINT `patch_tags_patchId_fkey` FOREIGN KEY (`patchId`) REFERENCES `patches`(`id`) ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey: patch_tags.tagId -> tags.id
ALTER TABLE `patch_tags` ADD CONSTRAINT `patch_tags_tagId_fkey` FOREIGN KEY (`tagId`) REFERENCES `tags`(`id`) ON DELETE CASCADE ON UPDATE CASCADE;
