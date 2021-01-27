# MO 日本語カードテキスト表示ツール
[Magic: The Gathering Online](http://www.mtgo.com/) で表示されているカードに対応して、日本語テキストを自動で表示するアプリです。

![Screenshot v2.0.0](https://github.com/fog-bank/mojp/blob/master/images/screenshot.png)

## 主な機能
* MO の Preview Pane に表示されているカードの日本語テキストを表示する
* カード名（日本語または英語）をコピーする
* [MTG Wiki](http://mtgwiki.com/) でカードを調べる
* カードが [Penny Dreadful](http://pdmtgo.com/) (PD) で使用可能かどうかを表示する
* [Scryfall](https://scryfall.com/) から日毎の平均価格を取得する

## 必要条件
Windows 7 SP1 以降  
.NET Framework 4.6 以上（Windows 10 なら標準でインストール済みです）

## 使い方
1. MO を起動し、COLLECTION 画面や対戦・トレード中に Preview Pane を表示するようにします（最小化しても OK）。
2. MO 上で調べたいカードにマウスカーソルを移動させます。Preview Pane にそのカードが表示されます。
3. Preview Pane に対応してこのアプリの表示が変わり、そのカードの日本語テキストが表示されます。

### 補足
* Preview Pane を表示するには、ACCOUNT 画面の「Display & Sound Settings」内の右下にある「Display Card Preview Window」にチェックを入れます。詳細は[こちら](https://github.com/fog-bank/mojp/wiki/Preview-Pane-%E3%82%92%E8%A1%A8%E7%A4%BA%E3%81%95%E3%81%9B%E3%82%8B%E6%96%B9%E6%B3%95)。
* カードをズームしたままマウスカーソルを移動した場合や、素早くマウスカーソルを移動した場合に、表示が変わらないことがあります。
* 拡張アート枠のプロモカードやトークン、紋章などには対応しておらず、正確なテキストが表示されません。詳細は[こちら](https://github.com/fog-bank/mojp/wiki/%E4%B8%8D%E5%85%B7%E5%90%88)。
* あくまで参考程度のご利用でお願い致します。このアプリを原因とする損害には責任を負いかねます。

### その他の機能
* 右上のツールバーにある地球アイコンのボタンをクリックすると、[MTG Wiki](http://mtgwiki.com/) の当該解説ページをブラウザで開きます。ツールバーに表示するボタンは変更可能です。
* 右クリックメニューから、カード名をコピーしたり、このアプリの設定を変更したりできます。
* 以下の機能はインターネットにバックグラウンドで接続します。不要な場合は設定から無効にしてください。
  * 起動時に https://fog-bank.github.io/mojp/ にアクセスして、新しいバージョンがリリースされているかどうかを自動で確認します。ZIP 版の場合はダウンロードまではしません。インストーラー版の場合は自動確認を無効にできません。
  * 起動時に [Penny Dreadful のカードリスト](http://pdmtgo.com/legal_cards.txt)をダウンロードします (1 日 1 回まで) 。
  * カードを表示するたび [Scryfall の検索 API](https://scryfall.com/docs/api/cards/search) にアクセスします (1 秒に 5 枚まで、各カード 1 日 1 回まで) 。

## 開発環境
Windows 10 October 2020 Update (Version 20H2)  
Visual Studio 2019 v16.8  
C# 9.0

### 仕組み
[UI Automation](https://msdn.microsoft.com/ja-jp/library/ms753388.aspx) API を利用して、Preview Pane 内のテキストを検索しています。メインロジックは [AutomationHandler.cs](https://github.com/fog-bank/mojp/blob/master/mojp/AutomationHandler.cs#L58) 内の CapturePreviewPane メソッド以下です。

## リファレンス
* MO で日本語テキストを表示する試みとして、[Magic Online 日本語化計画](http://www.royalcrab.net/wpx/?page_id=38)の影響を受けています。
* このアプリで表示する日本語テキストは、[WHISPER](http://whisper.wisdom-guild.net/) の検索結果をテキストファイルに保存したものに基づいています。
* アイコンの一部は Iconfinder \[[1](https://www.iconfinder.com/icons/6000/book_dictionary_learn_school_translate_icon#size=128), [2](https://www.iconfinder.com/icons/285680/camera_icon#size=16)\] を利用しています。

### 手動でカードデータを更新する方法
1. リポジトリから [appendix.xml](https://github.com/fog-bank/mojp/blob/master/mojp/appendix.xml) をダウンロードして、アプリケーションフォルダに保存しておく。
2. [WHISPER](http://whisper.wisdom-guild.net/) で、次元・現象・計略・策略以外のカードタイプにチェックを入れ、さらに一番下の出力形式をテキストにして検索する。
3. 表示されたテキストを、エンコード形式を Shift-JIS にして保存する。
4. 本アプリの設定画面を開き、一番下の開発用メニューから「検索結果テキストの読み込み」ボタンで保存したテキストを読み込む。
5. チェックマークが表示されたら完了。アプリケーションフォルダにある cards.xml が変更されているはずです。

カードセットを絞らない場合、2 万件以上のカードがヒットし、テキストのサイズは 7 MB を超えますので、この方法で頻繁に検索しないでください。

## 連絡先
Twitter: [@bank_fog](https://twitter.com/bank_fog)
