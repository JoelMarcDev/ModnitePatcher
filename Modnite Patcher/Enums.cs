namespace Port_a_Patch
{
    public enum PatchType
    {
        None,
        DecryptPak,
        ReplacePakFile,
        ReplaceBytes,
        DeleteFile
    }

    public enum PaddingType
    {
        NullBytes,
        Spaces
    }

    public enum GameFile
    {
        GameExecutable,
        BEExecutable,
        EACExecutable,
        FortniteLauncher,
        PakFile
    }
}
