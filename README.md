# MO 日本語カードテキスト表示ツール
[Magic: The Gathering Online](http://www.mtgo.com/) で表示されているカードに対応して、日本語テキストを表示するアプリです。

**MO のカード枠実装の変更に対応するため、v3.0.0 よりカードテキストの表示方法が以前から変更されました。現在動作テスト中です。**

![Screenshot latest](https://github.com/fog-bank/mojp/blob/master/docs/images/screenshot.png)
![Screenshot v2.0.0](https://github.com/fog-bank/mojp/blob/master/images/screenshot.png)

## 主な機能
* 右クリックしたカードの日本語テキストを表示する
* カード名（日本語または英語）をコピーする
* [MTG Wiki](http://mtgwiki.com/) でカードを調べる
* カードが [Penny Dreadful](http://pdmtgo.com/) (PD) で使用可能かどうかを表示する
* [Scryfall](https://scryfall.com/) から日毎の平均価格を取得する

## 必要条件
Windows 7 SP1 以降  
.NET Framework 4.6 以上（Windows 10 なら標準でインストール済みです）

## 使い方
1. MO と本アプリを起動します。
2. MO 上で調べたいカードに対して右クリックすることで、MO のメニューを表示させます。
3. それに反応してこのアプリの表示が変わり、そのカードの日本語テキストが表示されます。

### 補足
* MO のメニュー表示に反応するため、カードの右クリック以外でも表示が変わることがあります。
* 起動直後や、呪文や能力の解決中など、正常にカードテキストが表示されないことがあります。
* 一部のトークン、紋章などには対応しておらず、正確なテキストが表示されません。詳細は[こちら](https://github.com/fog-bank/mojp/wiki/%E4%B8%8D%E5%85%B7%E5%90%88)。
* あくまで参考程度のご利用でお願い致します。このアプリを原因とする損害には責任を負いかねます。

### その他の機能
* 右上のツールバーにある地球アイコンのボタンをクリックすると、[MTG Wiki](http://mtgwiki.com/) の当該解説ページをブラウザで開きます。ツールバーに表示するボタンは変更可能です。
* 右クリックメニューから、カード名をコピーしたり、このアプリの設定を変更したりできます。
* 以下の機能はインターネットにバックグラウンドで接続します。不要な場合は設定から無効にしてください。
  * 起動時に https://fog-bank.github.io/mojp/ にアクセスして、新しいバージョンがリリースされているかどうかを自動で確認します。ZIP 版の場合はダウンロードまではしません。インストーラー版の場合は自動確認を無効にできません。
  * 起動時に [Penny Dreadful のカードリスト](http://pdmtgo.com/legal_cards.txt)をダウンロードします (1 日 1 回まで) 。
  * カードを表示するたび [Scryfall の検索 API](https://scryfall.com/docs/api/cards/search) にアクセスします (1 秒に 5 枚まで、各カード 1 日 1 回まで) 。

## 開発環境
Windows 10 2022 Update (Version 22H2)  
Visual Studio 2022 v17.12  
C# 13

### 仕組み
[Microsoft UI Automation](https://learn.microsoft.com/ja-jp/dotnet/framework/ui-automation/) API を利用して、メニューを開いたときに発生するイベントをサブスクライブし、MO のプロセスに限り、メニュー内の UI テキストを検索しています。メインロジックは [AutomationHandler.cs](https://github.com/fog-bank/mojp/blob/master/mojp/AutomationHandler.cs) です。

## リファレンス
* MO で日本語テキストを表示する試みとして、[Magic Online 日本語化計画](https://k5.hatenablog.com/archive/category/MTGO_SUPPORT)の影響を受けています。
* このアプリで表示する日本語テキストは、[WHISPER](http://whisper.wisdom-guild.net/) の検索結果をテキストファイルに保存したものに基づいています。
* アイコンの一部は Iconfinder \[[1](https://www.iconfinder.com/icons/6000/book_dictionary_learn_school_translate_icon#size=128), [2](https://www.iconfinder.com/icons/285680/camera_icon#size=16)\] を利用しています。

### 手動でカードデータを更新する方法
1. リポジトリから [appendix.xml](https://github.com/fog-bank/mojp/blob/master/mojp/appendix.xml) をダウンロードして、アプリケーションフォルダに保存しておく。
2. [WHISPER](http://whisper.wisdom-guild.net/) で、次元・現象・計略・策略以外のカードタイプにチェックを入れ、さらに一番下の出力形式をテキストにして検索する。
注）検索結果が多すぎてタイムアウトするため、条件を絞って複数の出力に分割する必要がある場合があります。
3. 表示されたテキストを、エンコード形式を Shift-JIS にして保存する。
4. 本アプリの設定画面を開き、一番下の開発用メニューから「検索結果テキストの読み込み」ボタンで保存したテキストを読み込む。
5. チェックマークが表示されたら完了。アプリケーションフォルダにある cards.xml が変更されているはずです。

カードセットを絞らない場合、2 万件以上のカードがヒットし、テキストのサイズは 8 MB を超えますので、この方法で頻繁に検索しないでください。

## 連絡先
Website: https://fog-bank.github.io/mojp/  
Twitter: [@bank_fog](https://twitter.com/bank_fog)
