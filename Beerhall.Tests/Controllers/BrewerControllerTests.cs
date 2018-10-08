using System.Collections.Generic;
using System.Linq;
using Beerhall.Data;
using Beerhall.Models.Domain;
using Beerhall.Models.BrewerViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;
using Beerhall.Controllers;
using Beerhall.Tests.Data;

namespace Beerhall.Tests.Controllers
{
    public class BrewerControllerTest
    {
        private readonly BrewerController _controller;
        private readonly Mock<IBrewerRepository> _brewerRepository;
        private readonly Mock<ILocationRepository> _locationRepository;
        private readonly DummyApplicationDbContext _dummyContext;

        public BrewerControllerTest()
        {
            _dummyContext = new DummyApplicationDbContext();
            _brewerRepository = new Mock<IBrewerRepository>();
            _locationRepository = new Mock<ILocationRepository>();
            _controller = new BrewerController(_brewerRepository.Object, _locationRepository.Object)
            {
                TempData = new Mock<ITempDataDictionary>().Object
            };
        }

        #region -- Index --
        [Fact]
        public void Index_PassesOrderedListOfBrewersInViewResultModel()
        {
            _brewerRepository.Setup(m => m.GetAll()).Returns(_dummyContext.Brewers);
            IActionResult actionresult = _controller.Index();
            IList<Brewer> brewersInModel = (actionresult as ViewResult)?.Model as IList<Brewer>;
            Assert.Equal(3, brewersInModel?.Count);
            Assert.Equal("Bavik", brewersInModel?[0].Name);
            Assert.Equal("De Leeuw", brewersInModel?[1].Name);
            Assert.Equal("Duvel Moortgat", brewersInModel?[2].Name);
        }

        [Fact]
        public void Index_StoresTotalTurnoverInViewData()
        {
            _brewerRepository.Setup(m => m.GetAll()).Returns(_dummyContext.Brewers);
            IActionResult actionresult = _controller.Index();
            Assert.Equal(20050000, (actionresult as ViewResult)?.ViewData["TotalTurnover"]);
        }
        #endregion

        #region -- Edit GET --
        [Fact]
        public void Edit_PassesBrewerInEditViewModel()
        {
            _brewerRepository.Setup(m => m.GetBy(1)).Returns(_dummyContext.Bavik);
            IActionResult action = _controller.Edit(1);
            EditViewModel brewerEvm = (action as ViewResult)?.Model as EditViewModel;
            Assert.Equal("Bavik", brewerEvm?.Name);
        }

        [Fact]
        public void Edit_ReturnsSelectListOfGemeentenAndSelectedValue()
        {
            _brewerRepository.Setup(m => m.GetBy(1)).Returns(_dummyContext.Bavik);
            _locationRepository.Setup(m => m.GetAll()).Returns(_dummyContext.Locations);
            IActionResult action = _controller.Edit(1);
            ViewResult result = action as ViewResult;
            SelectList locationsInViewData = result?.ViewData["Locations"] as SelectList;
            Assert.Equal(3, locationsInViewData.Count());
        }

        [Fact]
        public void Edit_UnknownBrewer_ReturnsNotFound()
        {
            _brewerRepository.Setup(m => m.GetBy(1)).Returns((Brewer)null);
            IActionResult action = _controller.Edit(1);
            Assert.IsType<NotFoundResult>(action);
        }
        #endregion

        #region -- Edit POST --
        [Fact]
        public void Edit_ValidEdit_RedirectsToActionIndex()
        {
            _brewerRepository.Setup(m => m.GetBy(1)).Returns(_dummyContext.Bavik);
            EditViewModel brewerEvm = new EditViewModel(_dummyContext.Bavik)
            {
                Street = "nieuwe straat 1"
            };
            RedirectToActionResult action = _controller.Edit(brewerEvm, 1) as RedirectToActionResult;
            Assert.Equal("Index", action?.ActionName);
        }

        [Fact]
        public void Edit_ValidEdit_ChangesAndPersistsBrewer()
        {
            _brewerRepository.Setup(m => m.GetBy(1)).Returns(_dummyContext.Bavik);
            EditViewModel brewerEvm = new EditViewModel(_dummyContext.Bavik)
            {
                Street = "nieuwe straat 1"
            };
            _controller.Edit(brewerEvm, 1);
            Brewer bavik = _dummyContext.Bavik;
            Assert.Equal("Bavik", bavik.Name);
            Assert.Equal("nieuwe straat 1", bavik.Street);
            _brewerRepository.Verify(m => m.SaveChanges(), Times.Once());
        }

        [Fact]
        public void Edit_InvalidEdit_RedirectsToActionIndex()
        {
            _brewerRepository.Setup(m => m.GetBy(1)).Returns(_dummyContext.Bavik);
            EditViewModel brewerEvm = new EditViewModel(_dummyContext.Bavik) { Turnover = -1 };
            RedirectToActionResult action = _controller.Edit(brewerEvm, 1) as RedirectToActionResult;
            Assert.Equal("Index", action?.ActionName);
        }

        [Fact]
        public void Edit_InvalidEdit_DoesNotChangeNorPersistsBrewer()
        {
            _brewerRepository.Setup(m => m.GetBy(1)).Returns(_dummyContext.Bavik);
            EditViewModel brewerEvm = new EditViewModel(_dummyContext.Bavik) { Turnover = -1 };
            _controller.Edit(brewerEvm, 1);
            Brewer bavik = _dummyContext.Bavik;
            Assert.Equal("Bavik", bavik.Name);
            Assert.Equal("Rijksweg 33", bavik.Street);
            Assert.Equal(20000000, bavik.Turnover);
            _brewerRepository.Verify(m => m.SaveChanges(), Times.Never());
        }

        [Fact]
        public void Edit_ModelStateErrors_PassesViewModelAndViewDataToEditView()
        {
            _locationRepository.Setup(m => m.GetAll()).Returns(_dummyContext.Locations);
            EditViewModel brewerEvm = new EditViewModel(_dummyContext.Bavik);
            _controller.ModelState.AddModelError("", "Error message");
            ViewResult result = _controller.Edit(brewerEvm, 1) as ViewResult;
            Assert.Equal("Edit", result?.ViewName);
            Assert.Equal(brewerEvm, result?.Model);
            Assert.Equal(3, (result?.ViewData["Locations"] as SelectList)?.Count());
            Assert.True((bool)result?.ViewData["IsEdit"]);
        }

        [Fact]
        public void Edit_ModelStateErrors_DoesNotChangeNorPersistsBrewer()
        {
            Brewer bavik = _dummyContext.Bavik;
            _brewerRepository.Setup(m => m.GetBy(1)).Returns(bavik);
            EditViewModel newBrewerEvm = new EditViewModel(bavik)
            {
                Name = "New name"
            };
            _controller.ModelState.AddModelError("", "Error message");
            ViewResult result = _controller.Edit(newBrewerEvm, 1) as ViewResult;
            Assert.Equal("Bavik", bavik.Name);
            _brewerRepository.Verify(m => m.SaveChanges(), Times.Never());
        }
        #endregion

        #region -- Create GET --
        [Fact]
        public void Create_PassesNewBrewerInEditViewModel()
        {
            IActionResult action = _controller.Create();
            EditViewModel brewerEvm = (action as ViewResult)?.Model as EditViewModel;
            Assert.Null(brewerEvm?.Name);
        }

        [Fact]
        public void Create_ReturnsSelectListOfGemeentenWithNoSelectedValue()
        {
            _locationRepository.Setup(m => m.GetAll()).Returns(_dummyContext.Locations);
            IActionResult action = _controller.Create();
            ViewResult result = action as ViewResult;
            SelectList locationsInViewData = result?.ViewData["Locations"] as SelectList;
            Assert.Equal(3, locationsInViewData.Count());
            Assert.Null(locationsInViewData?.SelectedValue);
        }

        #endregion

        #region -- Create POST --
        [Fact]
        public void Create_ValidBrewer_RedirectsToActionIndex()
        {
            EditViewModel brewerEvm = new EditViewModel(new Brewer("Chimay")
            {
                Location = _dummyContext.Locations.Last(),
                Street = "TestStraat 10 ",
                Turnover = 8000000
            });
            RedirectToActionResult action = _controller.Create(brewerEvm) as RedirectToActionResult;
            Assert.Equal("Index", action?.ActionName);
        }

        [Fact]
        public void Create_ValidBrewer_CreatesAndPersistsBrewer()
        {
            _brewerRepository.Setup(m => m.Add(It.IsAny<Brewer>()));
            EditViewModel brewerEvm = new EditViewModel(_dummyContext.Bavik);
            _controller.Create(brewerEvm);
            _brewerRepository.Verify(m => m.Add(It.IsAny<Brewer>()), Times.Once());
            _brewerRepository.Verify(m => m.SaveChanges(), Times.Once());
        }

        [Fact]
        public void Create_InvalidBrewer_RedirectsToActionIndex()
        {
            EditViewModel brewerEvm = new EditViewModel(new Brewer("Chimay")) { Turnover = -1 };
            RedirectToActionResult action = _controller.Create(brewerEvm) as RedirectToActionResult;
            Assert.Equal("Index", action?.ActionName);
        }

        [Fact]
        public void Create_InvalidBrewer_DoesNotCreateNorPersistsBrewer()
        {
            _brewerRepository.Setup(m => m.Add(It.IsAny<Brewer>()));
            EditViewModel brewerEvm = new EditViewModel(new Brewer("Chimay")) { Turnover = -1 };
            RedirectToActionResult action = _controller.Create(brewerEvm) as RedirectToActionResult;
            _brewerRepository.Verify(m => m.Add(It.IsAny<Brewer>()), Times.Never());
            _brewerRepository.Verify(m => m.SaveChanges(), Times.Never());
        }

        [Fact]
        public void Create_ModelStateErrors_PassesViewModelAndViewDataToEditView()
        {
            _locationRepository.Setup(m => m.GetAll()).Returns(_dummyContext.Locations);
            EditViewModel brewerEvm = new EditViewModel(_dummyContext.Bavik);
            _controller.ModelState.AddModelError("", "Error message");
            ViewResult result = _controller.Create(brewerEvm) as ViewResult;
            Assert.Equal("Edit", result?.ViewName);
            Assert.Equal(brewerEvm, result?.Model);
            Assert.Equal(3, (result?.ViewData["Locations"] as SelectList)?.Count());
            Assert.False((bool)result?.ViewData["IsEdit"]);
        }

        [Fact]
        public void Create_ModelStateErrors_DoesNotCreateNorPersistsBrewer()
        {
            _brewerRepository.Setup(m => m.GetBy(1)).Returns(_dummyContext.Bavik);
            EditViewModel newBrewerEvm = new EditViewModel();
            _controller.ModelState.AddModelError("", "Error message");
            ViewResult result = _controller.Create(newBrewerEvm) as ViewResult;
            _brewerRepository.Verify(m => m.Add(It.IsAny<Brewer>()), Times.Never());
            _brewerRepository.Verify(m => m.SaveChanges(), Times.Never());
        }
        #endregion

        #region -- Delete GET --
        [Fact]
        public void Delete_PassesNameOfBrewerInViewData()
        {
            _brewerRepository.Setup(m => m.GetBy(1)).Returns(_dummyContext.Bavik);
            _brewerRepository.Setup(m => m.Delete(It.IsAny<Brewer>()));
            IActionResult action = _controller.Delete(1);
            Assert.Equal("Bavik", (action as ViewResult)?.ViewData["name"]);
        }

        [Fact]
        public void Delete_UnknownBrewer_ReturnsNotFound()
        {
            _brewerRepository.Setup(m => m.GetBy(1)).Returns((Brewer)null);
            IActionResult action = _controller.Delete(1);
            Assert.IsType<NotFoundResult>(action);
        }

        #endregion

        #region -- Delete POST --
        [Fact]
        public void Delete_ExistingBrewer_RedirectsToActionIndex()
        {
            _brewerRepository.Setup(m => m.GetBy(1)).Returns(_dummyContext.Bavik);
            _brewerRepository.Setup(m => m.Delete(It.IsAny<Brewer>()));
            RedirectToActionResult action = _controller.DeleteConfirmed(1) as RedirectToActionResult;
            Assert.Equal("Index", action?.ActionName);
        }

        [Fact]
        public void Delete_ExistingBrewer_DeletesBrewerAndPersistsChanges()
        {
            _brewerRepository.Setup(m => m.GetBy(1)).Returns(_dummyContext.Bavik);
            _brewerRepository.Setup(m => m.Delete(It.IsAny<Brewer>()));
            _controller.DeleteConfirmed(1);
            _brewerRepository.Verify(m => m.GetBy(1), Times.Once());
            _brewerRepository.Verify(m => m.Delete(It.IsAny<Brewer>()), Times.Once());
            _brewerRepository.Verify(m => m.SaveChanges(), Times.Once());
        }
        #endregion

    }
}
