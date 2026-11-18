using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // Prefer static readonly fields over constant array arguments

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Recordum.Data.Migrations
{
    /// <inheritdoc />
    public partial class ScaleAnchorsV220 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // GIO-23, spec §8. Tutto deve stare in UNA transazione: un DB a metà non deve esistere (lo prova
            // A_failure_at_the_last_step_leaves_the_2_19_database_untouched). Niente operazioni che EF su SQLite esegue
            // ricostruendo la tabella (DropColumn, AddCheckConstraint): nello script generato la ricostruzione chiude la
            // transazione (COMMIT; PRAGMA foreign_keys = 0; ...), e su tabelle con figli in cascata non la si vuole
            // vicino ai dati veri. Al loro posto ALTER TABLE ADD/DROP COLUMN, che SQLite (>= 3.35) esegue dentro la
            // transazione senza ricostruire. Lo schema risultante è quello del modello (il CHECK sta sulla colonna invece
            // che sulla tabella, con lo stesso nome).
            migrationBuilder.Sql(
                "ALTER TABLE scale_templates ADD COLUMN precision TEXT NOT NULL DEFAULT 'Integer' " +
                "CONSTRAINT ck_scale_templates_precision CHECK (precision IN ('Integer','Decimal'));");

            migrationBuilder.CreateTable(
                name: "scale_anchors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    scale_template_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    value = table.Column<int>(type: "INTEGER", nullable: false),
                    label = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    tag_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_ms = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scale_anchors", x => x.id);
                    table.ForeignKey(
                        name: "fk_scale_anchors_scale_templates_scale_template_id",
                        column: x => x.scale_template_id,
                        principalTable: "scale_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_scale_anchors_tags_tag_id",
                        column: x => x.tag_id,
                        principalTable: "tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "scale_anchors",
                columns: new[] { "id", "created_at", "label", "scale_template_id", "tag_id", "updated_at_ms", "value" },
                values: new object[,]
                {
                    { new Guid("00000003-0001-0000-0000-000000000001"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Hard pellets", new Guid("00000002-0000-0000-0000-000000000001"), null, 1778716800000L, 1 },
                    { new Guid("00000003-0001-0000-0000-000000000002"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Lumpy sausage", new Guid("00000002-0000-0000-0000-000000000001"), null, 1778716800000L, 2 },
                    { new Guid("00000003-0001-0000-0000-000000000003"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Cracked sausage", new Guid("00000002-0000-0000-0000-000000000001"), null, 1778716800000L, 3 },
                    { new Guid("00000003-0001-0000-0000-000000000004"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Smooth sausage", new Guid("00000002-0000-0000-0000-000000000001"), null, 1778716800000L, 4 },
                    { new Guid("00000003-0001-0000-0000-000000000005"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Soft blobs", new Guid("00000002-0000-0000-0000-000000000001"), null, 1778716800000L, 5 },
                    { new Guid("00000003-0001-0000-0000-000000000006"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Mushy", new Guid("00000002-0000-0000-0000-000000000001"), null, 1778716800000L, 6 },
                    { new Guid("00000003-0001-0000-0000-000000000007"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Liquid", new Guid("00000002-0000-0000-0000-000000000001"), null, 1778716800000L, 7 },
                    { new Guid("00000003-0002-0000-0000-000000000000"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "No bleeding", new Guid("00000002-0000-0000-0000-000000000002"), null, 1778716800000L, 0 },
                    { new Guid("00000003-0002-0000-0000-000000000001"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Streaks on tissue", new Guid("00000002-0000-0000-0000-000000000002"), null, 1778716800000L, 1 },
                    { new Guid("00000003-0002-0000-0000-000000000002"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Visible blood with stool", new Guid("00000002-0000-0000-0000-000000000002"), null, 1778716800000L, 2 },
                    { new Guid("00000003-0002-0000-0000-000000000003"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Bleeding without stool", new Guid("00000002-0000-0000-0000-000000000002"), null, 1778716800000L, 3 },
                    { new Guid("00000003-0003-0000-0000-000000000000"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "No pain", new Guid("00000002-0000-0000-0000-000000000003"), null, 1778716800000L, 0 },
                    { new Guid("00000003-0003-0000-0000-000000000001"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "1", new Guid("00000002-0000-0000-0000-000000000003"), null, 1778716800000L, 1 },
                    { new Guid("00000003-0003-0000-0000-000000000002"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "2", new Guid("00000002-0000-0000-0000-000000000003"), null, 1778716800000L, 2 },
                    { new Guid("00000003-0003-0000-0000-000000000003"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "3", new Guid("00000002-0000-0000-0000-000000000003"), null, 1778716800000L, 3 },
                    { new Guid("00000003-0003-0000-0000-000000000004"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "4", new Guid("00000002-0000-0000-0000-000000000003"), null, 1778716800000L, 4 },
                    { new Guid("00000003-0003-0000-0000-000000000005"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "5", new Guid("00000002-0000-0000-0000-000000000003"), null, 1778716800000L, 5 },
                    { new Guid("00000003-0003-0000-0000-000000000006"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "6", new Guid("00000002-0000-0000-0000-000000000003"), null, 1778716800000L, 6 },
                    { new Guid("00000003-0003-0000-0000-000000000007"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "7", new Guid("00000002-0000-0000-0000-000000000003"), null, 1778716800000L, 7 },
                    { new Guid("00000003-0003-0000-0000-000000000008"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "8", new Guid("00000002-0000-0000-0000-000000000003"), null, 1778716800000L, 8 },
                    { new Guid("00000003-0003-0000-0000-000000000009"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "9", new Guid("00000002-0000-0000-0000-000000000003"), null, 1778716800000L, 9 },
                    { new Guid("00000003-0003-0000-0000-000000000010"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Worst pain", new Guid("00000002-0000-0000-0000-000000000003"), null, 1778716800000L, 10 },
                    { new Guid("00000003-0004-0000-0000-000000000001"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Strongly disagree", new Guid("00000002-0000-0000-0000-000000000004"), null, 1778716800000L, 1 },
                    { new Guid("00000003-0004-0000-0000-000000000002"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Disagree", new Guid("00000002-0000-0000-0000-000000000004"), null, 1778716800000L, 2 },
                    { new Guid("00000003-0004-0000-0000-000000000003"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Neutral", new Guid("00000002-0000-0000-0000-000000000004"), null, 1778716800000L, 3 },
                    { new Guid("00000003-0004-0000-0000-000000000004"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Agree", new Guid("00000002-0000-0000-0000-000000000004"), null, 1778716800000L, 4 },
                    { new Guid("00000003-0004-0000-0000-000000000005"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Strongly agree", new Guid("00000002-0000-0000-0000-000000000004"), null, 1778716800000L, 5 },
                    { new Guid("00000003-0005-0000-0000-000000000001"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Very poor", new Guid("00000002-0000-0000-0000-000000000005"), null, 1778716800000L, 1 },
                    { new Guid("00000003-0005-0000-0000-000000000002"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Poor", new Guid("00000002-0000-0000-0000-000000000005"), null, 1778716800000L, 2 },
                    { new Guid("00000003-0005-0000-0000-000000000003"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Fair", new Guid("00000002-0000-0000-0000-000000000005"), null, 1778716800000L, 3 },
                    { new Guid("00000003-0005-0000-0000-000000000004"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Good", new Guid("00000002-0000-0000-0000-000000000005"), null, 1778716800000L, 4 },
                    { new Guid("00000003-0005-0000-0000-000000000005"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Excellent", new Guid("00000002-0000-0000-0000-000000000005"), null, 1778716800000L, 5 },
                    { new Guid("00000003-0006-0000-0000-000000000001"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Calm", new Guid("00000002-0000-0000-0000-000000000006"), null, 1778716800000L, 1 },
                    { new Guid("00000003-0006-0000-0000-000000000002"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "2", new Guid("00000002-0000-0000-0000-000000000006"), null, 1778716800000L, 2 },
                    { new Guid("00000003-0006-0000-0000-000000000003"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "3", new Guid("00000002-0000-0000-0000-000000000006"), null, 1778716800000L, 3 },
                    { new Guid("00000003-0006-0000-0000-000000000004"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "4", new Guid("00000002-0000-0000-0000-000000000006"), null, 1778716800000L, 4 },
                    { new Guid("00000003-0006-0000-0000-000000000005"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "5", new Guid("00000002-0000-0000-0000-000000000006"), null, 1778716800000L, 5 },
                    { new Guid("00000003-0006-0000-0000-000000000006"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "6", new Guid("00000002-0000-0000-0000-000000000006"), null, 1778716800000L, 6 },
                    { new Guid("00000003-0006-0000-0000-000000000007"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "7", new Guid("00000002-0000-0000-0000-000000000006"), null, 1778716800000L, 7 },
                    { new Guid("00000003-0006-0000-0000-000000000008"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "8", new Guid("00000002-0000-0000-0000-000000000006"), null, 1778716800000L, 8 },
                    { new Guid("00000003-0006-0000-0000-000000000009"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "9", new Guid("00000002-0000-0000-0000-000000000006"), null, 1778716800000L, 9 },
                    { new Guid("00000003-0006-0000-0000-000000000010"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Overwhelmed", new Guid("00000002-0000-0000-0000-000000000006"), null, 1778716800000L, 10 },
                    { new Guid("00000003-0007-0000-0000-000000000001"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Very low", new Guid("00000002-0000-0000-0000-000000000007"), null, 1778716800000L, 1 },
                    { new Guid("00000003-0007-0000-0000-000000000002"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Low", new Guid("00000002-0000-0000-0000-000000000007"), null, 1778716800000L, 2 },
                    { new Guid("00000003-0007-0000-0000-000000000003"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Neutral", new Guid("00000002-0000-0000-0000-000000000007"), null, 1778716800000L, 3 },
                    { new Guid("00000003-0007-0000-0000-000000000004"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Good", new Guid("00000002-0000-0000-0000-000000000007"), null, 1778716800000L, 4 },
                    { new Guid("00000003-0007-0000-0000-000000000005"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Very good", new Guid("00000002-0000-0000-0000-000000000007"), null, 1778716800000L, 5 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_scale_anchors_scale_value",
                table: "scale_anchors",
                columns: new[] { "scale_template_id", "value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_scale_anchors_tag_id",
                table: "scale_anchors",
                column: "tag_id");

            foreach (var step in ConversionSql()) migrationBuilder.Sql(step);

            migrationBuilder.Sql("ALTER TABLE scale_templates DROP COLUMN labels_json;");
            migrationBuilder.Sql("ALTER TABLE metric_definitions DROP COLUMN labels_json;");

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "schema_version",
                columns: new[] { "updated_at", "value" },
                values: new object[] { new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Utc), "2.20" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Non reversibile senza perdita: le posizioni, le copie per abitudine e le etichette sono state convertite
            // e le colonne tolte. Come CounterReclassV210, si riporta indietro solo schema_version.
            migrationBuilder.Sql(
                "UPDATE app_settings SET value = '2.19', updated_at = '2026-10-21 00:00:00' WHERE key = 'schema_version';");
        }

        /// <summary>Le etichette ufficiali tradotte alla 2.20, copiate da <c>Scale.*.Labels</c> di <c>Strings.resx</c> e
        /// <c>Strings.it.resx</c> (separatore <c>|</c>). Congelate: una migrazione è storia e non segue le traduzioni future
        /// (spec §8.4). Servono a riconoscere una copia per abitudine che è solo la traduzione dell'ufficiale.</summary>
        private static readonly (string Code, string En, string It)[] s_officialLabels =
        [
            ("bristol", "Hard pellets|Lumpy sausage|Cracked sausage|Smooth sausage|Soft blobs|Mushy|Liquid",
                "Palline dure|Salsiccia grumosa|Salsiccia con crepe|Salsiccia liscia|Pezzetti morbidi|Poltiglia|Liquida"),
            ("mayo-rectal-bleeding", "No bleeding|Streaks on tissue|Visible blood with stool|Bleeding without stool",
                "Nessun sanguinamento|Strisce sulla carta|Sangue visibile con le feci|Sanguinamento senza feci"),
            ("nrs-pain", "No pain|1|2|3|4|5|6|7|8|9|Worst pain",
                "Nessun dolore|1|2|3|4|5|6|7|8|9|Dolore massimo"),
            ("likert-5", "Strongly disagree|Disagree|Neutral|Agree|Strongly agree",
                "Fortemente in disaccordo|In disaccordo|Neutrale|D'accordo|Fortemente d'accordo"),
            ("sleep-quality", "Very poor|Poor|Fair|Good|Excellent",
                "Pessima|Scarsa|Discreta|Buona|Ottima"),
            ("stress-10", "Calm|2|3|4|5|6|7|8|9|Overwhelmed",
                "Calmo|2|3|4|5|6|7|8|9|Sopraffatto"),
            ("mood-5", "Very low|Low|Neutral|Good|Very good",
                "Molto basso|Basso|Neutro|Buono|Molto buono"),
        ];

        private static string SqlText(string s) => "'" + s.Replace("'", "''", StringComparison.Ordinal) + "'";

        private static string JsonList(string pipe) => SqlText(System.Text.Json.JsonSerializer.Serialize(pipe.Split('|')));

        /// <summary>Vero se due array JSON hanno gli stessi elementi nello stesso ordine (confronto per elementi, non
        /// come testo: lo stesso elenco può essere serializzato con escape diversi).</summary>
        private static string SameList(string a, string b) =>
            $"(json_array_length({a}) = json_array_length({b}) AND NOT EXISTS (SELECT 1 FROM json_each({a}) x " +
            $"JOIN json_each({b}) y ON y.key = x.key WHERE x.value IS NOT y.value))";

        /// <summary>La conversione dei dati (spec §8), in SQL puro dentro la transazione della migrazione. Usa due
        /// funzioni C# registrate sulla connessione da <see cref="SqliteFunctions"/>: <c>recordum_new_guid()</c> e
        /// <c>recordum_tag_slug(label)</c> (= <c>TagNames.FromLabel</c>). Le tabelle d'appoggio <c>_m220_*</c> stanno
        /// nel DB principale (cifrato), non in TEMP, e si tolgono alla fine.</summary>
        private static IEnumerable<string> ConversionSql()
        {
            const string now = "strftime('%Y-%m-%d %H:%M:%f', 'now')";
            const string nowMs = "CAST(unixepoch('subsec') * 1000 AS INTEGER)";

            // 0. Appoggio: le traduzioni congelate e le etichette normalizzate (JSON non valido o non array = nessuna
            //    etichetta: un'etichetta guasta non deve far fallire la migrazione dei dati veri).
            yield return """
                CREATE TABLE _m220_official (code TEXT NOT NULL, labels TEXT NOT NULL);
                CREATE TABLE _m220_scale (id TEXT PRIMARY KEY, labels TEXT);
                CREATE TABLE _m220_metric (id TEXT PRIMARY KEY, labels TEXT);
                CREATE TABLE _m220_new (scale_id TEXT PRIMARY KEY, metric_id TEXT NOT NULL UNIQUE, name TEXT NOT NULL,
                                        min_value INTEGER NOT NULL, max_value INTEGER NOT NULL, labels TEXT NOT NULL);
                CREATE TABLE _m220_value (id TEXT PRIMARY KEY, scale_id TEXT NOT NULL, value INTEGER NOT NULL);
                """;
            yield return "INSERT INTO _m220_official (code, labels) VALUES " + string.Join(",\n", s_officialLabels
                .SelectMany(o => new[] { $"({SqlText(o.Code)}, {JsonList(o.En)})", $"({SqlText(o.Code)}, {JsonList(o.It)})" })) + ";";
            yield return """
                INSERT INTO _m220_scale (id, labels)
                SELECT id, CASE WHEN json_valid(labels_json) THEN
                               CASE WHEN json_type(labels_json) = 'array' THEN labels_json END END
                FROM scale_templates;
                INSERT INTO _m220_metric (id, labels)
                SELECT id, CASE WHEN json_valid(labels_json) THEN
                               CASE WHEN json_type(labels_json) = 'array' AND json_array_length(labels_json) > 0
                                    THEN labels_json END END
                FROM metric_definitions WHERE labels_json IS NOT NULL;
                """;

            // (a) Copie per abitudine VERE: diverse, come elenco, dalle etichette della scala e (per un'ufficiale) dalla
            //     sua traduzione en e it. Diventano una scala dell'utente "‹scala› · ‹abitudine›" con gli stessi estremi.
            yield return $"""
                INSERT INTO _m220_new (scale_id, metric_id, name, min_value, max_value, labels)
                SELECT recordum_new_guid(), m.id, substr(s.name || ' · ' || coalesce(h.name, m.name), 1, 100),
                       s.min_value, s.max_value, mm.labels
                FROM metric_definitions m
                JOIN _m220_metric mm ON mm.id = m.id AND mm.labels IS NOT NULL
                JOIN scale_templates s ON s.id = m.scale_template_id
                JOIN _m220_scale ss ON ss.id = s.id
                LEFT JOIN habit_nodes h ON h.id = m.habit_node_id
                WHERE NOT (ss.labels IS NOT NULL AND {SameList("ss.labels", "mm.labels")})
                  AND NOT EXISTS (SELECT 1 FROM _m220_official o
                                  WHERE s.is_official = 1 AND o.code = s.code AND {SameList("o.labels", "mm.labels")});
                """;

            // (b) Etichette senza scala → scala dell'utente 1…N (almeno 1–2: una scala vuole Min < Max).
            yield return """
                INSERT INTO _m220_new (scale_id, metric_id, name, min_value, max_value, labels)
                SELECT recordum_new_guid(), m.id, substr(coalesce(h.name, m.name), 1, 100),
                       1, max(json_array_length(mm.labels), 2), mm.labels
                FROM metric_definitions m
                JOIN _m220_metric mm ON mm.id = m.id AND mm.labels IS NOT NULL
                LEFT JOIN habit_nodes h ON h.id = m.habit_node_id
                WHERE m.scale_template_id IS NULL;
                """;

            // (b) Stato senza scala né etichette: il ripiego 0..10 di StateTodayContent (posizioni 1..11) diventa una
            //     scala dell'utente "‹abitudine›" 0–10 senza scaglioni, sulla metrica che quella schermata registrava
            //     (la prima per sort_order; StateTodayContent preferisce una metrica con scala, qui non ce n'è).
            yield return """
                INSERT INTO _m220_new (scale_id, metric_id, name, min_value, max_value, labels)
                SELECT recordum_new_guid(), m.id, substr(h.name, 1, 100), 0, 10, '[]'
                FROM habit_nodes h
                JOIN metric_definitions m ON m.id = (SELECT p.id FROM metric_definitions p WHERE p.habit_node_id = h.id
                                                     ORDER BY p.sort_order, p.id LIMIT 1)
                WHERE h.habit_type = 'State' AND m.type = 'Integer'
                  AND NOT EXISTS (SELECT 1 FROM metric_definitions x WHERE x.habit_node_id = h.id
                                  AND (x.scale_template_id IS NOT NULL OR x.id IN (SELECT metric_id FROM _m220_new)));
                """;

            // Le scale nuove e il ripuntamento delle metriche.
            yield return $"""
                INSERT INTO scale_templates (id, code, name, min_value, max_value, labels_json, is_official, created_at)
                SELECT scale_id, 'user-' || lower(scale_id), name, min_value, max_value, labels, 0, {now} FROM _m220_new;
                INSERT INTO _m220_scale (id, labels) SELECT scale_id, labels FROM _m220_new;
                UPDATE metric_definitions
                SET scale_template_id = (SELECT n.scale_id FROM _m220_new n WHERE n.metric_id = metric_definitions.id)
                WHERE id IN (SELECT metric_id FROM _m220_new);
                """;

            // (c) Scaglioni delle scale dell'utente: l'etichetta in posizione p (json key = p − 1) va sul valore
            //     min + p − 1. Un'etichetta vuota o uguale al proprio numero ("2" sul 2) non crea lo scaglione.
            //     Poi i tag ricavati: trovati per nome (ix_tag_name è NOCASE) o creati.
            yield return $"""
                INSERT INTO scale_anchors (id, scale_template_id, value, label, tag_id, created_at, updated_at_ms)
                SELECT recordum_new_guid(), s.id, s.min_value + j.key, substr(trim(j.value), 1, 100), NULL, {now}, {nowMs}
                FROM scale_templates s
                JOIN _m220_scale ss ON ss.id = s.id
                JOIN json_each(ss.labels) j
                WHERE s.is_official = 0 AND j.type = 'text'
                  AND trim(j.value) <> '' AND trim(j.value) <> CAST(s.min_value + j.key AS TEXT);
                INSERT OR IGNORE INTO tags (id, name, color, created_at)
                SELECT recordum_new_guid(), slug, NULL, {now}
                FROM (SELECT DISTINCT recordum_tag_slug(a.label) AS slug FROM scale_anchors a
                      JOIN scale_templates s ON s.id = a.scale_template_id WHERE s.is_official = 0)
                WHERE slug IS NOT NULL;
                UPDATE scale_anchors
                SET tag_id = (SELECT t.id FROM tags t WHERE t.name = recordum_tag_slug(scale_anchors.label))
                WHERE scale_template_id IN (SELECT id FROM scale_templates WHERE is_official = 0);
                """;

            // (d) Il valore vero di ogni posizione salvata, calcolato una volta col min di prima di ogni allargamento:
            //     v = min + p − 1. Poi la scala si allarga fino a contenere valori e scaglioni (mai troncare).
            yield return """
                INSERT INTO _m220_value (id, scale_id, value)
                SELECT cv.id, s.id, s.min_value + cv.value_integer - 1
                FROM completion_values cv
                JOIN metric_definitions m ON m.id = cv.metric_definition_id
                JOIN scale_templates s ON s.id = m.scale_template_id
                WHERE cv.value_integer IS NOT NULL;
                UPDATE scale_templates
                SET max_value = max(max_value,
                        coalesce((SELECT max(v.value) FROM _m220_value v WHERE v.scale_id = scale_templates.id), max_value),
                        coalesce((SELECT max(a.value) FROM scale_anchors a WHERE a.scale_template_id = scale_templates.id), max_value)),
                    min_value = min(min_value,
                        coalesce((SELECT min(v.value) FROM _m220_value v WHERE v.scale_id = scale_templates.id), min_value));
                """;

            // (e) I valori: value_decimal nel testo che EF scrive per un decimale intero ("5.0"), value_integer a NULL;
            //     le metriche di scala diventano Decimal.
            yield return """
                UPDATE completion_values
                SET value_decimal = printf('%.1f', (SELECT v.value FROM _m220_value v WHERE v.id = completion_values.id)),
                    value_integer = NULL
                WHERE id IN (SELECT id FROM _m220_value);
                UPDATE metric_definitions SET type = 'Decimal' WHERE scale_template_id IS NOT NULL;
                """;

            yield return """
                DROP TABLE _m220_official;
                DROP TABLE _m220_scale;
                DROP TABLE _m220_metric;
                DROP TABLE _m220_new;
                DROP TABLE _m220_value;
                """;
        }
    }
}
