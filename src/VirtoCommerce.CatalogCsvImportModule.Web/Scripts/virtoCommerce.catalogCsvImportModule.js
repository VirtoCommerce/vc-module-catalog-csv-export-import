// Call this to register our module to main application
var moduleTemplateName = "virtoCommerce.catalogCsvImportModule";

if (AppDependencies != undefined) {
    AppDependencies.push(moduleTemplateName);
}

angular.module(moduleTemplateName, [])
    .config(['$stateProvider', '$urlRouterProvider',
        function ($stateProvider, $urlRouterProvider) {
        }
    ])
    .run(['$rootScope', 'platformWebApp.mainMenuService', 'platformWebApp.widgetService', 'platformWebApp.toolbarService', 'platformWebApp.pushNotificationTemplateResolver', 'platformWebApp.bladeNavigationService', '$state', 'virtoCommerce.catalogModule.catalogImportService', 'virtoCommerce.catalogModule.catalogExportService',
        function ($rootScope, mainMenuService, widgetService, toolbarService, pushNotificationTemplateResolver, bladeNavigationService, $state, catalogImportService, catalogExportService) {

            // NOTIFICATIONS
            // Export
            var menuExportTemplate =
            {
                priority: 900,
                satisfy: function (notify, place) { return place == 'menu' && notify.notifyType == 'CatalogCsvExport'; },
                template: 'Modules/$(VirtoCommerce.CatalogCsvImportModule)/Scripts/blades/export/notifications/menuExport.tpl.html',
                action: function (notify) { $state.go('workspace.pushNotificationsHistory', notify) }
            };
            pushNotificationTemplateResolver.register(menuExportTemplate);

            var historyExportTemplate =
            {
                priority: 900,
                satisfy: function (notify, place) { return place == 'history' && notify.notifyType == 'CatalogCsvExport'; },
                template: 'Modules/$(VirtoCommerce.CatalogCsvImportModule)/Scripts/blades/export/notifications/historyExport.tpl.html',
                action: function (notify) {
                    var blade = {
                        id: 'CatalogCsvExportDetail',
                        title: 'catalogCsvImportModule.blades.history.export.title',
                        subtitle: 'catalogCsvImportModule.blades.history.export.subtitle',
                        template: 'Modules/$(VirtoCommerce.CatalogCsvImportModule)/Scripts/blades/export/catalog-CSV-export.tpl.html',
                        controller: 'virtoCommerce.catalogCsvImportModule.catalogCSVexportController',
                        notification: notify
                    };
                    bladeNavigationService.showBlade(blade);
                }
            };
            pushNotificationTemplateResolver.register(historyExportTemplate);

            // Import
            var menuImportTemplate =
            {
                priority: 900,
                satisfy: function (notify, place) { return place == 'menu' && notify.notifyType == 'CatalogCsvImport'; },
                template: 'Modules/$(VirtoCommerce.CatalogCsvImportModule)/Scripts/blades/import/notifications/menuImport.tpl.html',
                action: function (notify) { $state.go('workspace.pushNotificationsHistory', notify) }
            };
            pushNotificationTemplateResolver.register(menuImportTemplate);

            var historyImportTemplate =
            {
                priority: 900,
                satisfy: function (notify, place) { return place == 'history' && notify.notifyType == 'CatalogCsvImport'; },
                template: 'Modules/$(VirtoCommerce.CatalogCsvImportModule)/Scripts/blades/import/notifications/historyImport.tpl.html',
                action: function (notify) {
                    var blade = {
                        id: 'CatalogCsvImportDetail',
                        title: 'catalogCsvImportModule.blades.history.import.title',
                        subtitle: 'catalogCsvImportModule.blades.history.import.title',
                        template: 'Modules/$(VirtoCommerce.CatalogCsvImportModule)/Scripts/blades/import/catalog-CSV-import.tpl.html',
                        controller: 'virtoCommerce.catalogCsvImportModule.catalogCSVimportController',
                        notification: notify
                    };
                    bladeNavigationService.showBlade(blade);
                }
            };
            pushNotificationTemplateResolver.register(historyImportTemplate);

            // IMPORT / EXPORT
            catalogImportService.register({
                name: 'VirtoCommerce CSV import',
                description: 'Native VirtoCommerce catalog data import from CSV',
                icon: 'fa fa-file-archive-o',
                controller: 'virtoCommerce.catalogCsvImportModule.catalogCSVimportWizardController',
                template: 'Modules/$(VirtoCommerce.CatalogCsvImportModule)/Scripts/blades/import/wizard/catalog-CSV-import-wizard.tpl.html'
            });

            catalogExportService.register({
                name: 'VirtoCommerce CSV export',
                description: 'Native VirtoCommerce catalog data export to CSV',
                icon: 'fa fa-file-archive-o',
                controller: 'virtoCommerce.catalogCsvImportModule.catalogCSVexportController',
                template: 'Modules/$(VirtoCommerce.CatalogCsvImportModule)/Scripts/blades/export/catalog-CSV-export.tpl.html'
            });
        }
    ]);
