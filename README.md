# MTGO Japanese Card Text Preview
[Magic: The Gathering Online](http://www.mtgo.com) で表示されているカードに対応して、日本語テキストを自動で表示するアプリです。

## 使い方
1. MO を起動し、COLLECTION 画面や、対戦・トレード中に「Preview Pane」を表示するようにします。
2. このアプリを起動し、右上のカメラアイコンのボタンを押します。自動検索機能が有効な場合は、押さなくても自動で処理します（下記参照）。
3. 準備完了と表示されれば OK です。
4. Preview Pane にカードが表示されるたび、このアプリの表示が変わり、対応する日本語テキストが表示されます。

### 補足
* Preview Pane を表示するには、ACCOUNT 画面の「Display & Sound Settings」内の右下にある「Display Card Preview Window」にチェックを入れます。
* v1.1.0 から Preview Pane の自動検索機能ができました。既定では 4 秒ごとに Preview Pane を探します。この処理により負荷が増す可能性があるので、設定からオフにできるようにしています。そうした場合は、Preview Pane が非表示になるたび、右上のカメラアイコンの**ボタンを押し直す必要があります**。
* Preview Pane が邪魔な場合は最小化してしまって問題ありません。
* トークンや紋章などには対応しておらず、何も表示されません。
* MO クライアントの多重起動にも対応していません。
* このアプリを原因とするプレイミスには責任を負いかねます。

### その他の機能
* 右クリックメニューからカード名をコピーできます。
* 右上の歯車アイコンのボタンから、このアプリで使用するフォントとそのサイズを設定できます。その他、ウィンドウサイズなどの設定は、%LocalAppData%\co3366353 フォルダに保存されます。

## 仕組み
[UI Automation](https://msdn.microsoft.com/ja-jp/library/ms753388.aspx) API を利用して、Preview Pane 内のテキストを検索しています。メインロジックは MainViewModel.cs 内の OnCapture メソッド以下です。

## リソース
* このアプリで表示する日本語テキストは、[WHISPER](http://whisper.wisdom-guild.net/) の検索結果をテキストファイルに保存したものに基づいています。
* このアプリのアイコンは [Iconfinder](https://www.iconfinder.com/icons/6000/book_dictionary_learn_school_translate_icon#size=128) のものを利用しています。