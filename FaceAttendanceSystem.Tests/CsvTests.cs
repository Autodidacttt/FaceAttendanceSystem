using FaceAttendanceSystem.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FaceAttendanceSystem.Tests
{
    [TestClass]
    public class CsvTests
    {
        [TestMethod]
        public void Escape_QuotesValuesWithCommasQuotesAndNewlines()
        {
            Assert.AreEqual("plain", Csv.Escape("plain"));
            Assert.AreEqual("\"a,b\"", Csv.Escape("a,b"));
            Assert.AreEqual("\"say \"\"hi\"\"\"", Csv.Escape("say \"hi\""));
            Assert.AreEqual("\"line1\nline2\"", Csv.Escape("line1\nline2"));
            Assert.AreEqual("", Csv.Escape(null));
        }

        [TestMethod]
        public void Escape_NeutralisesSpreadsheetFormulas()
        {
            Assert.AreEqual("'=1+1", Csv.Escape("=1+1"));
            Assert.AreEqual("'+cmd", Csv.Escape("+cmd"));
            Assert.AreEqual("'@SUM(A1)", Csv.Escape("@SUM(A1)"));
            Assert.AreEqual("'-x", Csv.Escape("-x"));
            Assert.AreEqual("-12.5", Csv.Escape("-12.5"));
        }

        [TestMethod]
        public void Rows_AreCrLfSeparated_AndBytesStartWithUtf8Bom()
        {
            var csv = new Csv().Row("a", 1).Row("b", 2.5);
            Assert.AreEqual("a,1\r\nb,2.5\r\n", csv.ToString());

            var bytes = csv.ToBytes();
            Assert.AreEqual(0xEF, bytes[0]);
            Assert.AreEqual(0xBB, bytes[1]);
            Assert.AreEqual(0xBF, bytes[2]);
        }
    }
}
