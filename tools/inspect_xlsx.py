import json
import re
import sys
import zipfile
import xml.etree.ElementTree as ET

NS = {"m": "http://schemas.openxmlformats.org/spreadsheetml/2006/main", "r": "http://schemas.openxmlformats.org/officeDocument/2006/relationships"}
REL_NS = {"p": "http://schemas.openxmlformats.org/package/2006/relationships"}

def column_index(reference):
    letters = re.match(r"[A-Z]+", reference).group(0)
    result = 0
    for letter in letters:
        result = result * 26 + ord(letter) - 64
    return result - 1

with zipfile.ZipFile(sys.argv[1]) as archive:
    shared = []
    if "xl/sharedStrings.xml" in archive.namelist():
        root = ET.fromstring(archive.read("xl/sharedStrings.xml"))
        for item in root.findall("m:si", NS):
            shared.append("".join(node.text or "" for node in item.iterfind(".//m:t", NS)))

    relationships = ET.fromstring(archive.read("xl/_rels/workbook.xml.rels"))
    targets = {node.attrib["Id"]: node.attrib["Target"] for node in relationships.findall("p:Relationship", REL_NS)}
    workbook = ET.fromstring(archive.read("xl/workbook.xml"))
    result = {}
    for sheet in workbook.find("m:sheets", NS):
        name = sheet.attrib["name"]
        target = targets[sheet.attrib[f"{{{NS['r']}}}id"]].lstrip("/")
        path = target if target.startswith("xl/") else "xl/" + target
        root = ET.fromstring(archive.read(path))
        rows = []
        for row in root.findall(".//m:sheetData/m:row", NS):
            values = []
            for cell in row.findall("m:c", NS):
                index = column_index(cell.attrib["r"])
                while len(values) <= index:
                    values.append(None)
                value = cell.find("m:v", NS)
                if value is None:
                    inline = cell.find("m:is/m:t", NS)
                    parsed = inline.text if inline is not None else None
                elif cell.attrib.get("t") == "s":
                    parsed = shared[int(value.text)]
                elif cell.attrib.get("t") == "b":
                    parsed = value.text == "1"
                else:
                    parsed = value.text
                values[index] = parsed
            rows.append(values)
        result[name] = rows
    if len(sys.argv) > 2 and sys.argv[2] == "summary":
        print(json.dumps({name: {"rows": len(rows), "sample": rows[:4]} for name, rows in result.items()}, ensure_ascii=False, indent=2))
    elif len(sys.argv) > 3 and sys.argv[2] == "sheet":
        print(json.dumps(result[sys.argv[3]], ensure_ascii=False, indent=2))
    elif len(sys.argv) > 2 and sys.argv[2] == "validate":
        def records(name):
            header, *rows = result[name]
            return [dict(zip(header, row)) for row in rows]
        competitions = records("Competitions")
        entries = records("TeamEntries")
        teams = records("Teams")
        print(json.dumps({
            "competitionTeamCounts": {row["CompetitionId"]: sum(x["CompetitionId"] == row["CompetitionId"] for x in entries) for row in competitions},
            "periodTypes": sorted(set(row["PeriodType"] for row in competitions)),
            "genders": sorted(set(row["Gender"] for row in teams)),
            "duplicateTeamNaturalKeys": sorted({f'{row["TeamName"]}|{row["Gender"]}' for row in teams if sum(x["TeamName"] == row["TeamName"] and x["Gender"] == row["Gender"] for x in teams) > 1}),
            "millerEntries": [{k: row[k] for k in ("CompetitionId", "SourceTeamId", "TeamName")} for row in entries if "MILLER" in row["TeamName"]],
        }, ensure_ascii=False, indent=2))
    elif len(sys.argv) > 3 and sys.argv[2] == "seed":
        def records(name):
            header, *rows = result[name]
            return [dict(zip(header, row)) for row in rows]
        source = {
            "season": [{"sourceId": int(x["SeasonId"]), "year": int(x["Year"]), "name": x["Name"], "active": x["Active"]} for x in records("Season")],
            "divisions": [{"sourceId": int(x["DivisionId"]), "name": x["Name"], "levelOrder": int(x["LevelOrder"]), "gender": x["Gender"], "active": x["Active"]} for x in records("Divisions")],
            "clubs": [{"sourceId": int(x["ClubId"]), "name": x["Name"], "active": x["Active"]} for x in records("Clubs")],
            "teams": [{"sourceId": int(x["TeamId"]), "sourceTeamId": int(x["SourceTeamId"]), "clubSourceId": int(x["ClubId"]), "name": x["TeamName"], "gender": x["Gender"], "active": x["Active"]} for x in records("Teams")],
            "venues": [{"sourceId": int(x["VenueId"]), "name": x["Name"], "active": x["Active"]} for x in records("Venues")],
            "competitions": [{"sourceId": int(x["CompetitionId"]), "name": x["Name"], "seasonSourceId": int(x["SeasonId"]), "divisionSourceId": int(x["DivisionId"]), "periodType": x["PeriodType"]} for x in records("Competitions")],
            "teamEntries": [{"sourceId": int(x["TeamEntryId"]), "competitionSourceId": int(x["CompetitionId"]), "teamSourceId": int(x["TeamId"]), "status": x["Status"]} for x in records("TeamEntries")],
        }
        with open(sys.argv[3], "w", encoding="utf-8", newline="\n") as output:
            json.dump(source, output, ensure_ascii=False, indent=2)
            output.write("\n")
        print(json.dumps({key: len(value) for key, value in source.items()}, indent=2))
    else:
        print(json.dumps(result, ensure_ascii=False, indent=2))
