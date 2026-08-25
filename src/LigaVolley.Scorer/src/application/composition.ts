import { db } from '../persistence/database';
import { ScorerController } from './scorerController';

export const createScorerController = () => new ScorerController(db);
