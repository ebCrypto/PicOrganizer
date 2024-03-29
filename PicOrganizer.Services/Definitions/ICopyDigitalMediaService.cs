﻿namespace PicOrganizer.Services
{
    public interface ICopyDigitalMediaService
    {
        Task AddMetaAndCopy(DirectoryInfo to);
        
        [Obsolete]
        void PropagateToOtherTargets();

        void RenameFileRemovingDiacritics(DirectoryInfo di);
    }
}