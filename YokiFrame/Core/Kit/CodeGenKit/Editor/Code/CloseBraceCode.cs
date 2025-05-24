namespace YokiFrame
{
    /// <summary>
    /// 后花括号
    /// </summary>
    public class CloseBraceCode : ICode
    {
        private readonly bool mSemicolon;

        public CloseBraceCode(bool semicolon)
        {
            mSemicolon = semicolon;
        }

        public void Gen(ICodeWriteKit writer)
        {
            var semicolonKey = mSemicolon ? ";" : string.Empty;
            writer.WriteLine("}" + semicolonKey);
        }
    }
}