namespace Port_a_Patch
{
    public static class TypeExtensions
    {
        public static string GetFileName(this GameFile file)
        {
            switch (file)
            {
                case GameFile.GameExecutable:
                    return "FortniteClient-Win64-Shipping.exe";

                case GameFile.FortniteLauncher:
                    return "FortniteLauncher.exe";

                case GameFile.BEExecutable:
                    return "FortniteClient-Win64-Shipping_BE.exe";

                case GameFile.EACExecutable:
                    return "FortniteClient-Win64-Shipping_EAC.exe";
            }

            return null;
        }
    }
}