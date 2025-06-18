define([
  "jQuery",
  "loading",
  "emby-input",
  "emby-button",
  "emby-select",
  "emby-checkbox",
], function ($, loading) {
  "use strict";

  var pluginUniqueId = "b1c2d3e4-f5a6-7b8c-9d0e-1f2a3b4c5d6e";

  function loadConfiguration(form) {
    ApiClient.getPluginConfiguration(pluginUniqueId).then(function (config) {
      var formElements = form.elements;
      
      formElements.chkIsEnabled.checked = config.IsEnabled || true;
      formElements.txtCollectionName.value = config.CollectionName || "Recommended Movies";
      formElements.selectRecommendationCount.value = config.RecommendationCount || 10;
      
      loading.hide();
    });
  }

  function onSubmit(ev) {
    ev.preventDefault();
    loading.show();

    var form = ev.currentTarget;
    var formElements = form.elements;

    ApiClient.getPluginConfiguration(pluginUniqueId).then(function (config) {
      config.IsEnabled = formElements.chkIsEnabled.checked;
      config.CollectionName = formElements.txtCollectionName.value || "Recommended Movies";
      config.RecommendationCount = parseInt(formElements.selectRecommendationCount.value) || 10;

      ApiClient.updatePluginConfiguration(pluginUniqueId, config).then(function (result) {
        Dashboard.processPluginConfigurationUpdateResult(result);
        Dashboard.alert("Settings saved.");
        loading.hide();
      });
    });

    return false;
  }

  return function init(view) {
    var form = view.querySelector("#recommendationConfigurationForm");
    form.addEventListener("submit", onSubmit);

    view.addEventListener("viewshow", function () {
      loading.show();
      loadConfiguration(form);
    });
  };
});