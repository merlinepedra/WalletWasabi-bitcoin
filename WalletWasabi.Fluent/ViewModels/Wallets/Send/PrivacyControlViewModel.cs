using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using DynamicData;
using DynamicData.Aggregation;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Exceptions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	[NavigationMetaData(Title = "Privacy Control")]
	public partial class PrivacyControlViewModel : RoutableViewModel
	{
		private readonly Wallet _wallet;
		private readonly SourceList<PocketViewModel> _pocketSource;
		private readonly ReadOnlyObservableCollection<PocketViewModel> _pockets;

		[AutoNotify] private decimal _stillNeeded;
		[AutoNotify] private bool _enoughSelected;

		public PrivacyControlViewModel(Wallet wallet, TransactionInfo transactionInfo, TransactionBroadcaster broadcaster)
		{
			_wallet = wallet;

			_pocketSource = new SourceList<PocketViewModel>();

			_pocketSource.Connect()
				.Bind(out _pockets)
				.Subscribe();

			var selected = _pocketSource.Connect()
				.AutoRefresh()
				.Filter(x => x.IsSelected);

			var selectedList = selected.AsObservableList();

			selected.Sum(x => x.TotalBtc)
				.Subscribe(x =>
				{
					StillNeeded = transactionInfo.Amount.ToDecimal(MoneyUnit.BTC) - x;
					EnoughSelected = StillNeeded <= 0;
				});

			StillNeeded = transactionInfo.Amount.ToDecimal(MoneyUnit.BTC);

			NextCommand = ReactiveCommand.CreateFromTask(
				async () =>
				{
					BuildTransactionResult transactionResult;
					var coins = selectedList.Items.SelectMany(x => x.Coins).ToArray();

					try
					{
						transactionResult = TransactionHelpers.BuildTransaction(_wallet, transactionInfo.Address, transactionInfo.Amount, transactionInfo.Labels, transactionInfo.FeeRate, coins, subtractFee: false);
					}
					catch (InsufficientBalanceException)
					{
						var dialog = new InsufficientBalanceDialogViewModel();
						var result = await NavigateDialog(dialog, NavigationTarget.DialogScreen);

						if (result.Result != true)
						{
							return;
						}

						transactionResult = TransactionHelpers.BuildTransaction(_wallet, transactionInfo.Address, transactionInfo.Amount, transactionInfo.Labels, transactionInfo.FeeRate, coins, subtractFee: true);
					}

					Navigate().To(new TransactionPreviewViewModel(wallet, transactionInfo, broadcaster, transactionResult));
				},
				this.WhenAnyValue(x => x.EnoughSelected));
		}

		public ReadOnlyObservableCollection<PocketViewModel> Pockets => _pockets;

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(isInHistory, disposables);

			if (!isInHistory)
			{
				var pockets = _wallet.Coins.GetPockets(_wallet.ServiceConfiguration.GetMixUntilAnonymitySetValue());

				foreach (var pocket in pockets)
				{
					_pocketSource.Add(new PocketViewModel(pocket));
				}
			}
		}
	}
}
