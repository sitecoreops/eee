# 🪶 Experience Edge Emu (EEE) 🪶

Lightweight Experience Edge emulator for local (offline) development and test automation.

## Features

- GraphQL endpoint
  - Experience Edge compatibility:
    - `site` queries.
    - `layout` queries.
    - `item` queries.
    - `search` queries **NOT SUPPORTED**.
  - Extras:
    - `crawl` mutation, crawls existing Experience Edge endpoint to seed emulator with data and media 🚀.
- Hosting media items.
- [GraphiQL UI](https://github.com/graphql-dotnet/server) accessible on `/`.
- Hot reloading data when files in data root is modified.
- Health endpoint `/healthz`.
- Docker multi platform images `docker image pull sitecoreops/eee` (runs on both Windows x64 and Linux x64).

## Data layout

Under your data root (default `./data`, configured with the `EMU__DATAROOTPATH` environment variable) the following rules apply:

```text
./data
   ├── /items/**/*.json (any structure supported, files must contain at least the required fields of type Item in the schema)
   ├── /site
         ├── /**/<language>.json (language specific siteInfo data such as dictionary and routes)
         ├── sitedata.json (SiteData.allSiteInfo is stored here )
   ├── /media/** (stored as the media path)
   ├── /*.graphqls (user schema files)
```

> TIP: Run a `crawl` mutation to get some data to learn from

## Using Experience Edge preview context id's (or local XM Cloud instances)

Add this patch to increase Sitecore GraphQL complexity configuration:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
  <sitecore>
    <api>
      <GraphQL>
        <defaults>
          <security>
            <publicService type="Sitecore.Services.GraphQL.Hosting.Security.GraphQLSecurity, Sitecore.Services.GraphQL">
              <complexityConfiguration type="GraphQL.Validation.Complexity.ComplexityConfiguration, GraphQL">
                <maxDepth>50</maxDepth>
                <maxComplexity>500000</maxComplexity>
              </complexityConfiguration>
            </publicService>
          </security>
        </defaults>
      </GraphQL>
    </api>
  </sitecore>
</configuration>
```

## Gotchas

Currently there a few limitations/gotchas, some may be fixed in the future:

1. You need to supply your own GraphQL schema in SDL format if you are using `... on <TEMPLATE_NAME>` in your queries.
   1. Download your own SDL from <https://edge.sitecorecloud.io/api/graphql/ide>.
   1. Place your SDL in `./data/user-schema.graphqls` (notice the file extension).
   1. Start `eee` and **fix any errors reported**, unfortunately the SDL downloaded from edge contains a few invalid lines that can't be parsed.
1. When running `eee` in Docker, you cannot crawl a local XM Cloud instance unless they share a Docker network.
1. Using the `maxWidth` and `maxHeight` on `src` property fields does nothing.
1. `SiteInfo.RoutesResult` only supports the `language` and `first` parameters, `excludedPaths` and `includePaths` does nothing and `after` throws `NotSupportedException`.
1. `SiteInfo.DictionaryResult` only supports the `language` and `first` parameters, `after` throws `NotSupportedException`.

## Quick start

You can run in Docker or download native binaries for Linux and Windows. Running with SSL is important if your head application also runs SSL to avoid the browser blocks loading media on non SSL urls.

### Docker

run without SSL:

`docker run -e "EMU__MEDIAHOST=http://localhost:5710" -p 5710:8080 sitecoreops/eee`

or with persistence:

`docker run -e "EMU__DATAROOTPATH=./data" -e "EMU__MEDIAHOST=http://localhost:5710" -p 5710:8080 sitecoreops/eee`

or with SSL:

1. Use [./compose.yml](./compose.yml) as reference, modify as needed.
1. Then `docker compose up -d`.
1. Make your machine trust the certificate, run `certutil -addstore -f "ROOT" ".\\docker\\caddy\\data\\caddy\\pki\\authorities\\local\\root.crt"`.

### Native binary

1. Download one of the binaries from <https://github.com/sitecoreops/eee/releases>.
1. Without SSL, run `.\eee.exe` (Windows) or `eee` (Linux).
1. For SSL, add the arguments:
   1. `--Kestrel:Endpoints:HttpsDefaultCert:Url=https://localhost:5711` to use the developer certificate from `dotnet dev-certs`.
   1. or `--Kestrel:Endpoints:Https:Url=https://localhost:5711 --Kestrel:Endpoints:Https:Certificate:Subject=localhost` to use your own.

### Usage

Run a `query` with `curl -k "https://localhost:5711/graphql" -H "Content-Type: application/json" --data-raw '{"query":"{item(path:\"/sitecore/content/tests/minimal\",language:\"en\"){id,path,name,displayName}}"}'`

Run a `crawl` mutation with `curl -k "https://localhost:5711/graphql" -H "Content-Type: application/json" --data-raw '{"query":"mutation{crawl(edgeContextId:\"<EDGE-CONTEXT-ID>\",languages:[\"en\",\"da-dk\",\"sv-se\"]){success,itemsProcessed,sitesProcessed,durationMs,message}}"}'`

Or open <https://localhost:5711> to use the GraphiQL UI.

When you have seeded `eee` with some data, change your local head application to use <https://localhost:5711/graphql> instead of a real Experience Edge url.
