﻿angular.module('virtoCommerce.contentModule')
.controller('virtoCommerce.contentModule.contentMainController', ['$scope', '$state', '$stateParams', 'virtoCommerce.contentModule.menus', 'virtoCommerce.contentModule.pages', 'virtoCommerce.contentModule.themes', 'virtoCommerce.contentModule.stores', 'platformWebApp.bladeNavigationService', 'platformWebApp.dialogService', 'platformWebApp.authService',
	function ($scope, $state, $stateParams, menus, pages, themes, stores, bladeNavigationService, dialogService, authService) {
	    $scope.selectedNodeId = null;

	    var blade = $scope.blade;

	    blade.initialize = function () {
	        blade.isLoading = true;
	        blade.currentEntities = [];

	        if (!angular.isUndefined($stateParams.storeId)) {
	            stores.get({ id: $stateParams.storeId }, function (data) {
	                blade.openThemes($stateParams.storeId, data.name);
	            });
	        };

	        stores.query({}, function (data) {
	            var loadCounter = data.length;

	            var finnalyFunction = function () {
	                blade.isLoading = --loadCounter;
	            };

	            blade.isLoading = _.any(data);

	            for (var i = 0; i < data.length; i++) {
	                stores.get({ id: data[i].id }, function (data) {
	                    var entity = {};
	                    entity.store = data;
	                    entity.listLinksCount = '...';
	                    entity.pagesCount = '...';
	                    entity.themesCount = '...';
	                    entity.blogsCount = '...';
	                    entity.defaultThemeName = undefined;
	                    entity.defaultTheme = undefined;
	                    entity.themes = [];

	                    menus.get({ storeId: entity.store.id }, function (data) {
	                        entity.listLinksCount = data.length;
	                    });

	                    pages.get({ storeId: entity.store.id }, function (data) {
	                        entity.pagesCount = _.reject(data, function (x) { return x.id.startsWith("blogs/"); }).length;
	                        pages.getFolders({ storeId: entity.store.id }, function (data) {
	                            entity.blogsCount = _.find(data.folders, function (x) { return x.folderName === "blogs" }).folders.length;
	                        });
	                    });

	                    themes.get({ storeId: entity.store.id }, function (data) {
	                        entity.themesCount = data.length;
	                        entity.themes = data;

	                        var defaultThemeNameProperty = _.find(entity.store.dynamicProperties, function (property) { return property.name === 'DefaultThemeName'; });
	                        if (defaultThemeNameProperty !== undefined && defaultThemeNameProperty.values !== undefined && defaultThemeNameProperty.values.length > 0) {
	                            entity.defaultThemeName = defaultThemeNameProperty.values[0].value;
	                            if (_.find(entity.themes, function (theme) { return theme.name === entity.defaultThemeName; }) !== undefined) {
	                                entity.defaultTheme = _.find(entity.themes, function (theme) { return theme.name === entity.defaultThemeName; });
	                            }
	                            else {
	                                entity.defaultThemeName = undefined;
	                            }
	                        }
	                        blade.currentEntities.push(entity);
	                    });

	                }).$promise.finally(finnalyFunction);
	            }
	        });
	    };


	    blade.refresh = function (storeId, requestType) {
	        var entity = _.find(blade.currentEntities, function (entity) { return entity.store.id === storeId });

	        switch (requestType) {
	            case 'menus':
	                menus.get({ storeId: storeId }, function (data) {
	                    entity.listLinksCount = data.length;
	                }, function (error) { bladeNavigationService.setError('Error ' + error.status, blade); });
	                break;

	            case 'pages':
	                pages.get({ storeId: storeId }, function (data) {
	                    entity.pagesCount = _.reject(data, function (page) { return page.id.startsWith("blogs/"); }).length;
	                }, function (error) { bladeNavigationService.setError('Error ' + error.status, blade); });
	                break;

	            case 'themes':
	                themes.get({ storeId: storeId }, function (data) {
	                    entity.themesCount = data.length;
	                    entity.themes = data;
	                }, function (error) { bladeNavigationService.setError('Error ' + error.status, blade); });
	                break;

	            case 'blogs':
	                pages.getFolders({ storeId: storeId }, function (data) {
	                    entity.blogsCount = _.find(data.folders, function (folder) { return folder.folderName === "blogs" }).folders.length;
	                }, function (error) { bladeNavigationService.setError('Error ' + error.status, blade); });
	                break;

	            case 'defaultTheme':
	                stores.get({ id: storeId }, function (data) {
	                    themes.get({ storeId: storeId }, function (themesList) {
	                        entity.themesCount = themesList.length;
	                        entity.themes = themesList;

	                        var defaultThemeNameProperty = _.find(data.dynamicProperties, function (property) { return property.name === 'DefaultThemeName'; });
	                        if (defaultThemeNameProperty !== undefined && defaultThemeNameProperty.values !== undefined && defaultThemeNameProperty.values.length > 0) {
	                            entity.defaultThemeName = defaultThemeNameProperty.values[0].value;
	                            var defaultTheme = _.find(entity.themes, function (theme) { return theme.name === entity.defaultThemeName; });
	                            if (defaultTheme !== undefined) {
	                                entity.defaultTheme = defaultTheme;
	                            }
	                            else {
	                                entity.defaultThemeName = undefined;
	                                if (_.where(data.dynamicProperties, { name: "DefaultThemeName" }).length > 0) {
	                                    angular.forEach(data.dynamicProperties, function (value, key) {
	                                        if (value.name === "DefaultThemeName") {
	                                            value.values[0] = { value: '' };
	                                        }
	                                    });
	                                }

	                                themesStores.update({ storeId: storeId }, data, function (data) {
	                                    entity.defaultThemeName = undefined;
	                                },
									function (error) { bladeNavigationService.setError('Error ' + error.status, blade); });
	                            }
	                        }
	                    }, function (error) { bladeNavigationService.setError('Error ' + error.status, blade); });
	                });

	            default:
	                break;
	        }
	    }

	    blade.onClose = function (closeCallback) {
	        closeChildrenBlades();
	        closeCallback();
	    };

	    function closeChildrenBlades() {
	        angular.forEach(blade.childrenBlades.slice(), function (child) {
	            bladeNavigationService.closeBlade(child);
	        });
	    }


	    blade.openThemes = function (storeId, storeName) {
	        closeChildrenBlades();

	        var newBlade = {
	            id: "themesListBlade",
	            storeId: storeId,
	            title: storeName + ' themes list',
	            subtitle: 'Themes List',
	            controller: 'virtoCommerce.contentModule.themesListController',
	            template: 'Modules/$(VirtoCommerce.Content)/Scripts/blades/themes/themes-list.tpl.html',
	        };
	        bladeNavigationService.showBlade(newBlade, blade);
	    }

	    blade.openPages = function (data) {
	        closeChildrenBlades();

	        var newBlade = {
	            id: "pagesListBlade",
	            storeId: data.store.id,
	            title: data.store.name + ' pages list',
	            type: 'pages',
	            subtitle: 'Pages List',
	            controller: 'virtoCommerce.contentModule.pagesListController',
	            template: 'Modules/$(VirtoCommerce.Content)/Scripts/blades/pages/pages-list.tpl.html',
	        };
	        bladeNavigationService.showBlade(newBlade, blade);
	    }

	    blade.openLinkLists = function (data) {
	        closeChildrenBlades();

	        var newBlade = {
	            id: "linkListBlade",
	            storeId: data.store.id,
	            title: data.store.name + ' link Lists',
	            subtitle: 'Link Lists',
	            controller: 'virtoCommerce.contentModule.linkListsController',
	            template: 'Modules/$(VirtoCommerce.Content)/Scripts/blades/menu/link-lists.tpl.html',
	        };
	        bladeNavigationService.showBlade(newBlade, blade);
	    }

	    blade.openBlogs = function (data) {
	        closeChildrenBlades();

	        var newBlade = {
	            id: "blogsListBlade",
	            storeId: data.store.id,
	            title: data.store.name + ' link Lists',
	            type: 'blogs',
	            subtitle: 'Blogs List',
	            controller: 'virtoCommerce.contentModule.pagesListController',
	            template: 'Modules/$(VirtoCommerce.Content)/Scripts/blades/pages/pages-list.tpl.html',
	        };
	        bladeNavigationService.showBlade(newBlade, blade);
	    }

	    blade.addNewTheme = function (data) {
	        closeChildrenBlades();

	        var newBlade = {
	            id: 'addTheme',
	            choosenStoreId: data.store.id,
	            currentEntity: {},
	            title: 'New theme asset',
	            subtitle: 'Create new theme',
	            controller: 'virtoCommerce.contentModule.addThemeController',
	            template: 'Modules/$(VirtoCommerce.Content)/Scripts/blades/themes/add-theme.tpl.html',
	        };
	        bladeNavigationService.showBlade(newBlade, blade);
	    }

	    blade.addNewPage = function (data) {
	        closeChildrenBlades();

	        var newBlade = {
	            id: 'addPageBlade',
	            choosenStoreId: data.store.id,
	            currentEntity: { name: null, content: null },
	            newPage: true,
	            title: 'Add new page',
	            subtitle: 'Create new page',
	            controller: 'virtoCommerce.contentModule.editPageController',
	            template: 'Modules/$(VirtoCommerce.Content)/Scripts/blades/pages/edit-page.tpl.html',
	        };
	        bladeNavigationService.showBlade(newBlade, blade);
	    }

	    blade.addNewLinkList = function (data) {
	        closeChildrenBlades();

	        var newBlade = {
	            id: 'addMenuLinkListBlade',
	            choosenStoreId: data.store.id,
	            newList: true,
	            title: 'Add new list',
	            subtitle: 'Create new list',
	            controller: 'virtoCommerce.contentModule.menuLinkListController',
	            template: 'Modules/$(VirtoCommerce.Content)/Scripts/blades/menu/menu-link-list.tpl.html',
	        };
	        bladeNavigationService.showBlade(newBlade, blade);
	    }

	    blade.addBlog = function (data) {
	        closeChildrenBlades();

	        var newBlade = {
	            id: 'openBlogNew',
	            choosenStoreId: data.store.id,
	            isNew: true,
	            entity: { name: undefined },
	            title: 'Add blog',
	            subtitle: 'Create new blog',
	            controller: 'virtoCommerce.contentModule.editBlogController',
	            template: 'Modules/$(VirtoCommerce.Content)/Scripts/blades/pages/edit-blog.tpl.html',
	        };
	        bladeNavigationService.showBlade(newBlade, blade);
	    }

	    blade.openTheme = function (data) {
	        closeChildrenBlades();

	        var newBlade = {
	            id: 'themeAssetListBlade',
	            choosenThemeId: data.defaultTheme.name,
	            choosenStoreId: data.store.id,
	            choosenTheme: data.defaultTheme,
	            title: 'Edit ' + data.defaultTheme.path,
	            subtitle: 'Theme asset list',
	            controller: 'virtoCommerce.contentModule.themeAssetListController',
	            template: 'Modules/$(VirtoCommerce.Content)/Scripts/blades/themes/theme-asset-list.tpl.html',
	        };
	        bladeNavigationService.showBlade(newBlade, blade);
	    }

	    blade.previewTheme = function (data) {
	        if (data.store.url !== undefined) {
	            window.open(data.store.url + '?previewtheme=' + data.defaultTheme.name, '_blank');
	        }
	        else {
	            var dialog = {
	                id: "noUrlInStore",
	                title: "Url is not set for store",
	                message: "Please, set store url, before preview theme!",
	                callback: function (remove) {

	                }
	            }
	            dialogService.showNotificationDialog(dialog);
	        }
	    }

	    $scope.openStoresModule = function () {
	        $state.go('workspace.storeModule');
	    };

	    blade.headIcon = 'fa-code';

	    blade.initialize();
	}]);
