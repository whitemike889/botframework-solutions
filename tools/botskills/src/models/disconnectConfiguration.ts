/**
 * Copyright(c) Microsoft Corporation.All rights reserved.
 * Licensed under the MIT License.
 */

import { ILogger } from '../logger';

export interface IDisconnectConfiguration {
    skillId: string;
    skillsFile: string;
    outFolder: string;
    noRefresh: boolean;
    cognitiveModelsFile: string;
    languages: string[];
    luisFolder: string;
    dispatchFolder: string;
    lgOutFolder: string;
    lgLanguage: string;
    logger?: ILogger;
}
