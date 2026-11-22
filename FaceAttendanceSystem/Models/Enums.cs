namespace FaceAttendanceSystem.Models
{
    public enum UserRole
    {
        Admin = 1,
        Teacher = 2,
        Student = 3
    }

    public enum AttendanceStatus
    {
        Present = 1,
        Absent = 2
    }

    public enum AttendanceMethod
    {
        Face = 1,
        Manual = 2
    }
}
