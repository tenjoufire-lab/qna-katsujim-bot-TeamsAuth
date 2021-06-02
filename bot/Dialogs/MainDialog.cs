// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.BotBuilderSamples.Dialogs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QnABotAllFeatures;

namespace Microsoft.BotBuilderSamples.Dialogs
{
    public class MainDialog : LogoutDialog
    {
        protected readonly ILogger Logger;
        private readonly UserState _userState;


        public MainDialog(IBotServices services, IConfiguration configuration, ILogger<MainDialog> logger, UserState userState)
            : base(nameof(MainDialog), configuration["ConnectionName"], userState)
        {
            _userState = userState;
            Logger = logger;

            AddDialog(new OAuthPrompt(
                nameof(OAuthPrompt),
                new OAuthPromptSettings
                {
                    ConnectionName = ConnectionName,
                    Text = "ログインしてください",
                    Title = "ログイン",
                    Timeout = 300000, // User has 5 minutes to login (1000 * 60 * 5)
                }));

            //AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));
            AddDialog(new RootDialog(services, configuration));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                PromptStepAsync,
                LoginStepAsync,
                //DisplayTokenPhase1Async,
                //DisplayTokenPhase2Async,
            }));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> PromptStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var loginstateAccessors = _userState.CreateProperty<LoginState>(nameof(LoginState));
            var loginstate = await loginstateAccessors.GetAsync(stepContext.Context, null, cancellationToken);
            if (loginstate != null)
            {
                //QnA Dialog
                if (loginstate.isLogin) return await stepContext.ReplaceDialogAsync("root", null, cancellationToken);
            }
            return await stepContext.BeginDialogAsync(nameof(OAuthPrompt), null, cancellationToken);
        }

        private async Task<DialogTurnResult> LoginStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // Get the token from the previous step. Note that we could also have gotten the
            // token directly from the prompt itself. There is an example of this in the next method.
            var tokenResponse = (TokenResponse)stepContext.Result;
            if (tokenResponse?.Token != null)
            {
                // Pull in the data from the Microsoft Graph.
                var client = new SimpleGraphClient(tokenResponse.Token);
                var me = await client.GetMeAsync();
                var title = !string.IsNullOrEmpty(me.JobTitle) ?
                            me.JobTitle : "Unknown";

                var accessor = _userState.CreateProperty<LoginState>(nameof(LoginState));
                await accessor.SetAsync(stepContext.Context, new LoginState(true), cancellationToken);
                //await stepContext.Context.SendActivityAsync($"You're logged in as {me.DisplayName} ({me.UserPrincipalName}); you job title is: {title}");
                await stepContext.Context.SendActivityAsync($"こんにちは！{me.DisplayName} ({me.UserPrincipalName})さん");

                //return await stepContext.PromptAsync(nameof(ConfirmPrompt), new PromptOptions { Prompt = MessageFactory.Text("Would you like to view your token?") }, cancellationToken);
                return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
            }

            await stepContext.Context.SendActivityAsync(MessageFactory.Text("ログインに失敗しました、もう一度お試しください"), cancellationToken);
            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }

        private async Task<DialogTurnResult> DisplayTokenPhase1Async(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            await stepContext.Context.SendActivityAsync(MessageFactory.Text("Thank you."), cancellationToken);

            var result = (bool)stepContext.Result;
            if (result)
            {
                // Call the prompt again because we need the token. The reasons for this are:
                // 1. If the user is already logged in we do not need to store the token locally in the bot and worry
                // about refreshing it. We can always just call the prompt again to get the token.
                // 2. We never know how long it will take a user to respond. By the time the
                // user responds the token may have expired. The user would then be prompted to login again.
                //
                // There is no reason to store the token locally in the bot because we can always just call
                // the OAuth prompt to get the token or get a new token if needed.
                return await stepContext.BeginDialogAsync(nameof(OAuthPrompt), cancellationToken: cancellationToken);
            }

            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }

        private async Task<DialogTurnResult> DisplayTokenPhase2Async(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var tokenResponse = (TokenResponse)stepContext.Result;
            if (tokenResponse != null)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Here is your token {tokenResponse.Token}"), cancellationToken);
            }

            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }
    }
}
