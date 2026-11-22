using System;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;

namespace FaceAttendanceSystem.Infrastructure
{
    public static class DbErrors
    {
        /// <summary>True when the save failed because of a unique index / primary key violation.</summary>
        public static bool IsUniqueViolation(DbUpdateException ex)
        {
            for (Exception e = ex; e != null; e = e.InnerException)
            {
                var sql = e as SqlException;
                if (sql != null && (sql.Number == 2601 || sql.Number == 2627)) return true;
            }
            return false;
        }
    }
}
