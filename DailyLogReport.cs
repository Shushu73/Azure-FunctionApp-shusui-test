using Azure.Identity;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Azure_FunctionApp_shusui_test
{
    public class DailyLogReport
    {
        private readonly ILogger<DailyLogReport> _logger;
        private readonly string WorkspaceId = Environment.GetEnvironmentVariable( "DailyLogReport_WorkspaceId" );
        private readonly string TenantId = Environment.GetEnvironmentVariable( "DailyLogReport_TenantId" );

        public DailyLogReport( ILogger<DailyLogReport> logger )
        {
            _logger = logger;
        }



        [Function( "DailyLogReport" )]
        public async Task Run( [TimerTrigger( "0 */1 * * * *" )] TimerInfo myTimer )
        {
            var html = new StringBuilder();
            html.AppendLine( $"<html><body><h1 style='font-family:Arial;'>Daily Log Report Sky{DateTime.Now:dd.MM.yyyy}</h1>" );

            // Drei Abschnitte — jeder nutzt die 3 Helfer
            html.AppendLine( await MacheAbschnitt( "Requests" ) );
            html.AppendLine( await MacheAbschnitt( "Dependencies" ) );
            html.AppendLine( await MacheAbschnitt( "Exceptions" ) );

            html.AppendLine( "</body></html>" );

            await File.WriteAllTextAsync( "ausgabe.html", html.ToString() );
            _logger.LogInformation( "HTML geschrieben." );
        }

        // Verbindet die 3 Helfer für einen Abschnitt
        private async Task<string> MacheAbschnitt( string titel )
        {
            var query = GetQuery( titel );        // ① : KQL
            var daten = await LoadReport( query ); // ②  Daten holen
            var html = BuildHtml( titel, daten );  // ③  HTML bauen
            return html;
        }


        // ①  gibt die passende KQL je nach Titel
        private string GetQuery( string titel )
        {
            if ( titel == "Requests" )
                return "AppRequests | where TimeGenerated > ago(24h) | project TimeGenerated, Name, Success, ResultCode, DurationMs | order by TimeGenerated desc | take 20";
            if ( titel == "Dependencies" )
                return "AppDependencies | where TimeGenerated > ago(24h) | project TimeGenerated, Target, Success, DurationMs | order by TimeGenerated desc | take 20";
            // Exceptions
            return "AppExceptions | where TimeGenerated > ago(24h) | project TimeGenerated, Type, OuterMessage, Method | order by TimeGenerated desc | take 20";
        }

        // ②  holt die Daten aus Azure
        private async Task<LogsTable> LoadReport( string query )
        {
            var client = new LogsQueryClient(
                new DefaultAzureCredential( new DefaultAzureCredentialOptions { TenantId = TenantId } ) );

            var response = await client.QueryWorkspaceAsync(
                WorkspaceId, query, new QueryTimeRange( TimeSpan.FromHours( 24 ) ) );

            return response.Value.Table;
        }

        // ③  baut die HTML-Tabelle
        private string BuildHtml( string titel, LogsTable table )
        {
            var sb = new StringBuilder();
            sb.AppendLine( $"<h2 style='font-family:Arial;color:#2c3e50;'>{titel}</h2>" );
            sb.AppendLine( "<table style='border-collapse:collapse;font-family:Arial;font-size:13px;'>" );

            // Kopfzeile
            sb.AppendLine( "<tr>" );
            foreach ( var col in table.Columns )
                sb.AppendLine( $"<th style='background-color:#2c3e50;color:white;border:1px solid #ddd;padding:6px;'>{col.Name}</th>" );
            sb.AppendLine( "</tr>" );

            // Datenzeilen
            foreach ( var row in table.Rows ) {
                sb.AppendLine( "<tr>" );
                for ( int i = 0; i < row.Count; i++ )
                    sb.AppendLine( $"<td style='border:1px solid #ddd;padding:6px;'>{row[i]?.ToString()}</td>" );
                sb.AppendLine( "</tr>" );
            }

            sb.AppendLine( "</table>" );
            return sb.ToString();
        }


        // E-Mail (kommt später)
        private async Task SendMail( string html ) { }
    }
}