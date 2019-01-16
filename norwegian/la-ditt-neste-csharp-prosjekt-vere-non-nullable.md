# La ditt neste C#-prosjekt vere non-nullable

Som du sikkert veit har `null` sidan 1965 hatt skulda for det som i beste fall er masse ekstra, åndsfattig kode, og i verste fall programvarekræsj. Det var Tony Hoare som kom opp med denne "innovasjonen" i 1965, og han har i seinare tid bedt om unnskyldning for ein slik [_milliard-dollar-tabbe_](https://en.wikipedia.org/wiki/Null_pointer#History). 

Her er eit eksempel på den ekstra nullsjekkinga vi må gjere. Vi vil eigentleg at `requiredField` ikkje skal kunne vere `null`, men vi har ingen måte å la kompilatoren forlange det. Så vi må sjekke sjølv. 

![](https://github.com/torhovland/torhovland.github.io/raw/master/img/non-nullable/legacy-myclass.png)

Du synest kanskje det ser meiningslaust ut å berre byte ut ein exception (`NullReferenceException`) med ein annan (`ArgumentNullException`) slik, men det er det ikkje. Ein `NullReferenceException` kan oppstå djupt inne i koden, og har ikkje med seg informasjon om kva som ikkje skulle ha vore `null`, så ein blir nødt til å inspisere eller debugge koden for å finne ut kva som er feil. Det er spesielt vanskeleg i produksjonskode der PDB-filene med debug-info enten manglar eller har upresise linjenumre på grunn av optimalisert kode. Ein `ArgumentNullException` er langt meir hjelpsom på kva du som utviklar treng å fikse, enten den har med seg ei konkret feilmelding som i eksempelet over, eller berre ei henvisning til argumentet som var `null`.

Dessverre er det ofte ikkje råd å vite om ein referanse kan vere `null` eller ikkje, og då må vi velge om vi skal legge inn `null`-sjekking overalt i koden, eller rett og slett stole på at enkelte ting ikkje kjem til å vere `null`. Her er eit eksempel på det fyrste: 

![](https://github.com/torhovland/torhovland.github.io/raw/master/img/non-nullable/excessive-null-checking.png)

Som profesjonelle utviklarar er det kanskje forventa at vi skal sjekke all kode på denne måten? Problemet er berre at mykje av denne feilsjekkinga faktisk _er_ overflødig. Det kan godt hende at både `_someDependency`, `Foo` og `Bar` blir handtert til aldri å vere `null`. Men vi får ingen hjelp av kompilatoren til å fortelje oss det. Og sjølv om vi skulle sjekke det manuelt og konkludere med at vi ikkje treng ein nullsjekk her, så kan jo det lett endre seg i framtida, dersom `SomeDependency` blir modifisert.

Det er litt av ei smørje vi har hamna i, og det er nesten litt rart at vi utviklarar har funne oss i dette så lenge utan gateopprør! Det er lett å forstå at dette blir kalla ein _milliard-dollar-tabbe_. Eigentleg er nok det ein stor underdrivelse.

Dei fleste programmeringsspråk har dessverre arva `null`, men ein del språk, som Haskell, OCaml, Scala, F#, Elm og Rust, har valt det robuste alternativet [_option types_](https://en.wikipedia.org/wiki/Option_type). Ragnhild Aalvik demonstrerer korleis dette ser ut i Elm [her](https://elm.christmas/2018/5).

Option types er ei god løysing så lenge det er designa inn i språket frå starten av. Å skulle ettermontere det i språk som C# og Java er derimot problematisk, fordi det innfører ein ny måte å representere og sjekke manglande verdiar på, samtidig som ein framleis må handtere `null` i samkvem med eldre kode. Likevel er det nettopp dette som har skjedd i Java 8, med den nye `Optional`-typen.

I Kotlin har ein klart å omfamne `null` som ein del av språket og likevel oppnå robust handtering av dei. Løysinga er eigentleg ganske enkel når ein ser litt nærare på kva vi ynskjer å oppnå: at det ikkje skal gå an å dereferere ein referanse som er `null`. Greit, så dermed må alle referansar eksplisitt vere eitt av følgande:

- Ikkje nullbar (_non-nullable_).
- Nullbar, og dermed ikkje lov å dereferere utan ein tilhøyrande `null`-sjekk.

Vi har faktisk på ein måte hatt denne moglegheita i C# i [ti år](https://blogs.msmvps.com/peterritchie/2008/07/21/working-with-resharper-s-external-annotation-xml-files/) allereie, ved hjelp av [ReSharper Annotation Framework](https://www.jetbrains.com/resharper/features/code_analysis.html). Ved å markere kode med attributt, kan ReSharper hjelpe oss der typesystemet til C# ikkje har kunna. Her er eit eksempel:

![](https://github.com/torhovland/torhovland.github.io/raw/master/img/non-nullable/resharper-myclass.png)

Om eg no forsøker å sette `requiredField` til `null`, så vil ReSharper åtvare meg mot det:

![](https://github.com/torhovland/torhovland.github.io/raw/master/img/non-nullable/resharper-required-not-null.png)

Tilsvarande vil eg få ei åtvaring dersom eg prøver å dereferere `optionalField` utan å sjekke for `null`:

![](https://github.com/torhovland/torhovland.github.io/raw/master/img/non-nullable/resharper-null-reference.png)

Dette er vel og bra, men langt frå perfekt. For det fyrste er det berre nyttige hint og ikkje kompileringsfeil. For det andre er det avhengig av eit tredjeparts verktøy. For det tredje blir du ikkje tvungen til å markere all kode, så problemet blir aldri heilt borte. Og ReSharper har ein _optimistisk_ analyse som default, som betyr at når attributta manglar vurderer den det som viktigare å unngå falske alarmar enn å påpeike alle potensielle kodeproblem. For det fjerde fører alle desse attributta til ein del støy i koden.

Heldigvis treng vi ikkje lenger å bekymre oss for noko av det, for i C# 8 er _nullable reference types_ innebygd i språket. Namnet er litt forvirrande, for referansetypar har jo alltid vore nullbare. Det er jo heile problemet. Det nye er at dei no også kan vere _ikkje-nullbare_, og at det er det dei i utgangspunktet blir. Og så kan du altså gjere dei eksplisitt nullbare ved hjelp av `Nullable<T>` eller `T?`. Av hensyn til bakoverkompatibilitet med eksisterande kode er dette ein oppførsel som naturleg nok ikkje er påslått i utgangspunktet. Eg kan slå det på med eit flagg i kodefilene eller i prosjektfila. Eg må også konfigurere at eg vil bruke C# 8, som per no er i beta. I tillegg ynskjer eg at åtvaringane frå kompilatoren skal oppgraderast til kompileringsfeil:

```
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>8.0</LangVersion>
    <NullableReferenceTypes>true</NullableReferenceTypes>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

</Project>
```

Her er den same klassen som før, men legg merke til kor mykje reinare dette blir utan dei ekstra attributta:

![](https://github.com/torhovland/torhovland.github.io/raw/master/img/non-nullable/csharp-myclass.png)

Når vi forsøker å sette `requiredField` til `null`, får vi ei liknande melding som før, men denne gongen frå kompilatoren:

![](https://github.com/torhovland/torhovland.github.io/raw/master/img/non-nullable/csharp-required-not-null.png)

Det ser vi også når vi forsøker å dereferere `optionalField`:

![](https://github.com/torhovland/torhovland.github.io/raw/master/img/non-nullable/csharp-null-reference.png)

Betyr dette at tida er inne for å gå over all gamal kode og skru på nullable reference types? Ikkje nødvendigvis, for det er ein betydeleg jobb å fikse alle resulterande kompileringsfeil, og ikkje utan risiko for å innføre nye feil. Om du er motivert for det, sjå [her](https://praeclarum.org/2018/12/17/nullable-reference-types.html) og [her](https://codeblog.jonskeet.uk/2018/04/21/first-steps-with-nullable-reference-types/) for ein smakebit på kva du har i vente.

Men for nye prosjekt er det ingen tvil. Der er tida inne for å gravlegge `NullReferenceException` ein gong for alle[^interop]!

[^interop]: Dette er under føresetnad at vi snakkar om applikasjonskode der du kan skru på C# 8 overalt. For bibliotek der du må forvente at enkelte brukarar er på eldre versjon av C#, eller som kanskje berre ignorerer dei nye kompileringsåtvaringane, må du vurdere om du skal ta med ekstra `null`-sjekking, som før. Meir om dette [her](https://csharp.christiannagel.com/2018/06/20/nonnullablereferencetypes/).
