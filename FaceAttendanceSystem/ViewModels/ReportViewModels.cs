using System;
using System.Collections.Generic;
using System.Web.Mvc;

namespace FaceAttendanceSystem.ViewModels
{
    /// <summary>Query string of /Reports and /Reports/Export.</summary>
    public class ReportQuery
    {
        /// <summary>subject | student | date</summary>
        public string Type { get; set; }
        public int? SubjectId { get; set; }
        public string Roll { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
    }

    public class ReportViewModel
    {
        public ReportQuery Query { get; set; } = new ReportQuery();
        public IEnumerable<SelectListItem> SubjectOptions { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string Message { get; set; }
        public bool HasResult { get; set; }

        public List<StudentAttendanceRow> SubjectRows { get; set; }
        public List<SubjectAttendanceRow> StudentRows { get; set; }
        public List<SessionRow> SessionRows { get; set; }
    }
}
