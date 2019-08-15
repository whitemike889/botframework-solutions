/**
 * Copyright(c) Microsoft Corporation.All rights reserved.
 * Licensed under the MIT License.
 */

import * as program from 'commander';
import { join, resolve } from 'path';
import { RefreshSkill } from './functionality';
import { ConsoleLogger, ILogger} from './logger';
import { IRefreshConfiguration } from './models';
import { sanitizePath, validatePairOfArgs } from './utils';

function showErrorHelp(): void {
    program.outputHelp((str: string): string => {
        logger.error(str);

        return '';
    });
    process.exit(1);
}

const logger: ILogger = new ConsoleLogger();

program.Command.prototype.unknownOption = (flag: string): void => {
    logger.error(`Unknown arguments: ${flag}`);
    showErrorHelp();
};

program
    .name('botskills refresh')
    .description('Refresh the connected skills.')
    .option('--cs', 'Determine your assistant project structure to be a CSharp-like structure')
    .option('--ts', 'Determine your assistant project structure to be a TypeScript-like structure')
    .option('--luisFolder [path]', '[OPTIONAL] Path to the folder containing your Skills\' .lu files (defaults to \'./deployment/resources/skills/en\' inside your assistant folder)')
    .option('--dispatchFolder [path]', '[OPTIONAL] Path to the folder containing your assistant\'s \'.dispatch\' file (defaults to \'./deployment/resources/dispatch/en\' inside your assistant folder)')
    .option('--outFolder [path]', '[OPTIONAL] Path for any output file that may be generated (defaults to your assistant\'s root folder)')
    .option('--lgOutFolder [path]', '[OPTIONAL] Path for the LuisGen output (defaults to a \'service\' folder inside your assistant\'s folder)')
    .option('--cognitiveModelsFile [path]', '[OPTIONAL] Path to your Cognitive Models file (defaults to \'cognitivemodels.json\' inside your assistant\'s folder)')
    .option('--verbose', '[OPTIONAL] Output detailed information about the processing of the tool')
    .action((cmd: program.Command, actions: program.Command): undefined => undefined);

const args: program.Command = program.parse(process.argv);

if (process.argv.length < 3) {
    program.help();
}
logger.isVerbose = args.verbose;

// Validation of arguments
// cs and ts validation
const csAndTsValidationResult: string = validatePairOfArgs(args.cs, args.ts);
if (csAndTsValidationResult) {
    logger.error(
        csAndTsValidationResult.replace('{0}', 'cs')
            .replace('{1}', 'ts')
    );
    process.exit(1);
}

const projectLanguage: string = args.cs ? 'cs' : 'ts';
// Initialize an instance of IConnectConfiguration to send the needed arguments to the connectSkill function
const configuration: Partial<IRefreshConfiguration> = {
    lgLanguage: projectLanguage
};

// outFolder validation -- the const is needed for reassuring 'configuration.outFolder' is not undefined
const outFolder: string = args.outFolder ? sanitizePath(args.outFolder) : resolve('./');
configuration.outFolder = outFolder;

// cognitiveModelsFile validation
const cognitiveModelsFilePath: string = args.cognitiveModelsFile || join(configuration.outFolder, (args.ts ? join('src', 'cognitivemodels.json') : 'cognitivemodels.json'));
configuration.cognitiveModelsFile = cognitiveModelsFilePath;
// luisFolder validation
configuration.luisFolder = args.luisFolder ? sanitizePath(args.luisFolder) : join(configuration.outFolder, 'Deployment', 'Resources', 'Skills');

// dispatchFolder validation
configuration.dispatchFolder = args.dispatchFolder ? sanitizePath(args.dispatchFolder) : join(configuration.outFolder, 'Deployment', 'Resources', 'Dispatch');

// lgOutFolder validation
configuration.lgOutFolder = args.lgOutFolder ? sanitizePath(args.lgOutFolder) : join(configuration.outFolder, (args.ts ? join('src', 'Services') : 'Services'));

configuration.logger = logger;
// End of arguments validation

new RefreshSkill(<IRefreshConfiguration> configuration, logger).refreshSkill();
