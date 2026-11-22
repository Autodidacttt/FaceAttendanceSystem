namespace FaceAttendanceSystem.Migrations
{
    using System.Data.Entity.Migrations;

    /// <summary>
    /// Data-only migration: the app no longer forces a password change at sign-in,
    /// so clear the flag on every existing account (including the seeded demo accounts).
    /// </summary>
    public partial class NoForcedPasswordChange : DbMigration
    {
        public override void Up()
        {
            Sql("UPDATE dbo.[User] SET MustChangePassword = 0 WHERE MustChangePassword = 1");
        }

        public override void Down()
        {
            // Nothing to restore: which accounts had the flag set is not recorded.
        }
    }
}
