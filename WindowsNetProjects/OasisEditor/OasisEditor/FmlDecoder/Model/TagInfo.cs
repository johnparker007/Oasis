namespace MfmeFmlDecoder.Model
{
    public class TagInfo
    {
        public int Length { get; set; }
        public string AttributeName { get; set; }

        public ValueRole Role { get; set; }
        public TagType Type { get; set; }

        public byte[] DefaultValues { get; set; }

        public TagInfo(int length, string attributeName, byte[] defaultValues, ValueRole role = ValueRole.RAW)
        {
            Length = length;
            AttributeName = attributeName;
            Role = role;
            DefaultValues = defaultValues;
        }
    }
}
