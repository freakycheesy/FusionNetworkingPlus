using Epic.OnlineServices;
using Epic.OnlineServices.Platform;

namespace Riptide.Transports.Eos {
    public static class EOSSDK {
        public static PlatformInterface PlatformInterface {
            get; private set;
        }
        private static string productVersion => LabFusion.FusionMod.Version.Revision.ToString();
        private const string 
            productName = "Fusion",
            productId = "9d11e30b5f604b68af59bbfd376afcf8",
            sandboxId = "5706f401d54347d08132af6eb267c8d2",
            deploymentId = "d763553e75954e988f659ac8dbe9b9b1",
            clientId = "xyza7891PlpZwqvEZZ92EH15ffehM7j9",
            clientSecret = "kqJJwObDU0xIz0rnh6sqy7J9x0/jlWsLoqS4BN804bQ";

        public static EpicAccountId LocalUserAccountId {
            get; private set;
        }

        public static ProductUserId LocalUserProductId {
            get; private set;
        }

        public static void SetupPlatform() {
            var initializeOptions = new InitializeOptions() {
                ProductName = productName,
                ProductVersion = productVersion
            };

            var initializeResult = PlatformInterface.Initialize(ref initializeOptions);
            if (initializeResult != Epic.OnlineServices.Result.Success) {
                throw new Exception("Failed to initialize platform: " + initializeResult);
            }

            // The SDK outputs lots of information that is useful for debugging.
            // Make sure to set up the logging interface as early as possible: after initializing.
            Epic.OnlineServices.Logging.LoggingInterface.SetLogLevel(Epic.OnlineServices.Logging.LogCategory.AllCategories, Epic.OnlineServices.Logging.LogLevel.VeryVerbose);
            Epic.OnlineServices.Logging.LoggingInterface.SetCallback((ref Epic.OnlineServices.Logging.LogMessage logMessage) =>
            {
                Console.WriteLine(logMessage.Message);
            });

            var options = new Options() {
                ProductId = productId,
                SandboxId = sandboxId,
                DeploymentId = deploymentId,
                ClientCredentials = new ClientCredentials() {
                    ClientId = clientId,
                    ClientSecret = clientSecret
                }
            };

            PlatformInterface = PlatformInterface.Create(ref options);
            if (PlatformInterface == null) {
                throw new Exception("Failed to create platform");
            }
        }

        public static void LogIn() {
            Auth();
            Connect();
        }

        private static void Connect() {
            var loginOptions = new Epic.OnlineServices.Connect.LoginOptions();

            loginOptions.UserLoginInfo = new Epic.OnlineServices.Connect.UserLoginInfo() {
                DisplayName = LocalPlayer.Username,
            };

            loginOptions.Credentials = new Epic.OnlineServices.Connect.Credentials() {
                Type = ExternalCredentialType.DeviceidAccessToken,
                Token = loginCredentialToken,
            };


            PlatformInterface.GetConnectInterface().Login(ref loginOptions, null, null);
        }

        private static string loginCredentialId;
        private static string loginCredentialToken;

        private static void Auth() {
            var loginCredentialType = Epic.OnlineServices.Auth.LoginCredentialType.AccountPortal;
            /// These fields correspond to <see cref="Epic.OnlineServices.Auth.Credentials.Id" /> and <see cref="Epic.OnlineServices.Auth.Credentials.Token" />,
            /// and their use differs based on the login type. For more information, see <see cref="Epic.OnlineServices.Auth.Credentials" />
            /// and the Auth Interface documentation.
            loginCredentialId = null;
            loginCredentialToken = null;

            var authInterface = PlatformInterface.GetAuthInterface();
            if (authInterface == null) {
                throw new System.Exception("Failed to get auth interface");
            }

            var loginOptions = new Epic.OnlineServices.Auth.LoginOptions() {
                Credentials = new Epic.OnlineServices.Auth.Credentials() {
                    Type = loginCredentialType,
                    Id = loginCredentialId,
                    Token = loginCredentialToken
                },
                // Change these scopes to match the ones set up on your product on the Developer Portal.
                ScopeFlags = Epic.OnlineServices.Auth.AuthScopeFlags.BasicProfile | Epic.OnlineServices.Auth.AuthScopeFlags.FriendsList | Epic.OnlineServices.Auth.AuthScopeFlags.Presence
            };

            // Ensure platform tick is called on an interval, or the following call will never callback.
            authInterface.Login(ref loginOptions, null, (ref Epic.OnlineServices.Auth.LoginCallbackInfo loginCallbackInfo) => {
                if (loginCallbackInfo.ResultCode == Epic.OnlineServices.Result.Success) {
                    System.Console.WriteLine("Login succeeded");
                    LocalUserAccountId = loginCallbackInfo.LocalUserId;
                }
                else if (Epic.OnlineServices.Common.IsOperationComplete(loginCallbackInfo.ResultCode)) {
                    System.Console.WriteLine("Login failed: " + loginCallbackInfo.ResultCode);
                }
            });
        }
    }
}
