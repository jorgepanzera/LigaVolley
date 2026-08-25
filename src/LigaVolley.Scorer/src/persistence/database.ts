import Dexie, { type Table } from 'dexie';
import type { LocalEvent, MatchSheetRecord, SessionRecord, SnapshotRecord } from '../domain/types';
export interface AppMeta {
  key: string;
  value: string;
}
export class ScorerDatabase extends Dexie {
  appMeta!: Table<AppMeta, string>;
  matchSheets!: Table<MatchSheetRecord, number>;
  sessions!: Table<SessionRecord, string>;
  snapshots!: Table<SnapshotRecord, number>;
  events!: Table<LocalEvent, string>;
  constructor(name = 'LigaVolleyScorer') {
    super(name);
    this.version(1).stores({
      appMeta: 'key',
      matchSheets: 'matchId,sheetUuid',
      sessions: 'sessionUuid,matchId,sheetUuid,status',
      snapshots: 'matchId',
      events: 'eventUuid,[sessionUuid+sequence],[sessionUuid+syncStatus]',
    });
  }
}
export const db = new ScorerDatabase();
export async function deviceId(database = db) {
  return database.transaction('rw', database.appMeta, async () => {
    const found = await database.appMeta.get('deviceId');
    if (found) return found.value;
    const value = crypto.randomUUID();
    await database.appMeta.put({ key: 'deviceId', value });
    return value;
  });
}
