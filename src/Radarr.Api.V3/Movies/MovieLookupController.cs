using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.ImportLists.ImportExclusions;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Organizer;
using Radarr.Http;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Movies
{
    [V3ApiController("movie/lookup")]
    public class MovieLookupController : RestController<MovieResource>
    {
        private readonly ISearchForNewMovie _searchProxy;
        private readonly IProvideMovieInfo _movieInfo;
        private readonly IBuildFileNames _fileNameBuilder;
        private readonly INamingConfigService _namingService;
        private readonly IMapCoversToLocal _coverMapper;
        private readonly IConfigService _configService;
        private readonly IImportListExclusionService _importListExclusionService;

        public MovieLookupController(ISearchForNewMovie searchProxy,
                                 IProvideMovieInfo movieInfo,
                                 IBuildFileNames fileNameBuilder,
                                 INamingConfigService namingService,
                                 IMapCoversToLocal coverMapper,
                                 IConfigService configService,
                                 IImportListExclusionService importListExclusionService)
        {
            _movieInfo = movieInfo;
            _searchProxy = searchProxy;
            _fileNameBuilder = fileNameBuilder;
            _namingService = namingService;
            _coverMapper = coverMapper;
            _configService = configService;
            _importListExclusionService = importListExclusionService;
        }

        [NonAction]
        public override ActionResult<MovieResource> GetResourceByIdWithErrorHandler(int id)
        {
            throw new NotImplementedException();
        }

        protected override MovieResource GetResourceById(int id)
        {
            throw new NotImplementedException();
        }

        [HttpGet("tmdb")]
        [Produces("application/json")]
        public MovieResource SearchByTmdbId(int tmdbId)
        {
            var availDelay = _configService.AvailabilityDelay;
            var result = new Movie { MovieMetadata = _movieInfo.GetMovieInfo(tmdbId).Item1 };
            var translation = result.MovieMetadata.Value.Translations.FirstOrDefault(t => t.Language == (Language)_configService.MovieInfoLanguage);
            return result.ToResource(availDelay, translation);
        }

        [HttpGet("imdb")]
        [Produces("application/json")]
        public MovieResource SearchByImdbId(string imdbId)
        {
            var result = new Movie { MovieMetadata = _movieInfo.GetMovieByImdbId(imdbId) };

            var availDelay = _configService.AvailabilityDelay;
            var translation = result.MovieMetadata.Value.Translations.FirstOrDefault(t => t.Language == (Language)_configService.MovieInfoLanguage);
            return result.ToResource(availDelay, translation);
        }

        [HttpGet]
        [Produces("application/json")]
        public IEnumerable<MovieResource> Search([FromQuery] string term)
        {
            var searchResults = _searchProxy.SearchForNewMovie(term);

            return MapToResource(searchResults);
        }

        private IEnumerable<MovieResource> MapToResource(IEnumerable<Movie> movies)
        {
            var movieInfoLanguage = (Language)_configService.MovieInfoLanguage;
            var availDelay = _configService.AvailabilityDelay;
            var namingConfig = _namingService.GetConfig();

            var listExclusions = _importListExclusionService.All();

            foreach (var currentMovie in movies)
            {
                var translation = currentMovie.MovieMetadata.Value.Translations.FirstOrDefault(t => t.Language == movieInfoLanguage);
                var resource = currentMovie.ToResource(availDelay, translation);

                _coverMapper.ConvertToLocalUrls(resource.Id, resource.Images);

                var poster = currentMovie.MovieMetadata.Value.Images.FirstOrDefault(c => c.CoverType == MediaCoverTypes.Poster);
                if (poster != null)
                {
                    resource.RemotePoster = poster.RemoteUrl;
                }

                resource.Folder = _fileNameBuilder.GetMovieFolder(currentMovie, namingConfig);

                resource.IsExcluded = listExclusions.Any(e => e.TmdbId == resource.TmdbId);

                yield return resource;
            }
        }
    }
}
